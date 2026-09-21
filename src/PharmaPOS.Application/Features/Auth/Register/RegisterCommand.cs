using FluentValidation;
using MediatR;
using PharmaPOS.Application.Common.Authorization;
using PharmaPOS.Application.Common.Identity;
using PharmaPOS.Application.Common.Tenancy;
using PharmaPOS.Application.Features.Tenancy;
using PharmaPOS.Domain.Exceptions;

namespace PharmaPOS.Application.Features.Auth.Register;

/// <summary>
/// Self-registers a new pharmacy: creates the Keycloak account (with the <c>tenant_id</c>
/// attribute and the pharmacy-owner realm role), the tenant directory record and the owner's
/// database membership, then returns tokens so the new owner lands signed in.
/// </summary>
public sealed record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string PharmacyName) : IRequest<LoginResponse>;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        // Mirrors the realm password policy (length(12) + upper/lower/digit); Keycloak
        // re-validates server-side, this only fails fast with a friendly message.
        RuleFor(command => command.Password)
            .NotEmpty()
            .MinimumLength(12)
            .MaximumLength(256)
            .Matches("[A-Z]").WithMessage("Password must contain an upper-case letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lower-case letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");

        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.PharmacyName).NotEmpty().MaximumLength(160);
    }
}

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, LoginResponse>
{
    private readonly IKeycloakClient _keycloak;
    private readonly IPlatformTenantStore _tenantStore;
    private readonly ITenantProvisioning _provisioning;

    public RegisterCommandHandler(
        IKeycloakClient keycloak,
        IPlatformTenantStore tenantStore,
        ITenantProvisioning provisioning)
    {
        _keycloak = keycloak;
        _tenantStore = tenantStore;
        _provisioning = provisioning;
    }

    public async Task<LoginResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var tenantIdentifier = await UniqueTenantIdentifierAsync(request.PharmacyName, cancellationToken);

        // Keycloak first: it owns email uniqueness and the password policy. Compensate the
        // created account if the database side fails afterwards.
        var keycloakUserId = await _keycloak.CreateUserAsync(
            email,
            email,
            request.FirstName.Trim(),
            request.LastName.Trim(),
            request.Password,
            tenantIdentifier,
            RealmRoles.PharmacyOwner,
            cancellationToken);

        try
        {
            await _tenantStore.CreateAsync(
                new TenantDto(
                    Id: tenantIdentifier,
                    Identifier: tenantIdentifier,
                    Name: request.PharmacyName.Trim(),
                    Status: TenantStatus.Active,
                    PlanCode: null),
                cancellationToken);

            await _provisioning.ProvisionOwnerAsync(
                tenantIdentifier,
                email,
                $"{request.FirstName.Trim()} {request.LastName.Trim()}",
                keycloakSubject: null,
                cancellationToken);
        }
        catch
        {
            await _keycloak.DeleteUserAsync(keycloakUserId, email);
            throw;
        }

        var tokens = await _keycloak.TokenAsync(email, request.Password, cancellationToken);

        return new LoginResponse(
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.ExpiresInSeconds);
    }

    private async Task<string> UniqueTenantIdentifierAsync(string pharmacyName, CancellationToken cancellationToken)
    {
        var slug = new string(pharmacyName
                .Trim()
                .ToLowerInvariant()
                .Select(character => char.IsLetterOrDigit(character) ? character : '-')
                .ToArray())
            .Trim('-');

        while (slug.Contains("--"))
        {
            slug = slug.Replace("--", "-");
        }

        if (slug.Length < 2)
        {
            slug = "pharmacy";
        }

        if (slug.Length > 48)
        {
            slug = slug[..48].TrimEnd('-');
        }

        var candidate = slug;
        for (var attempt = 2; ; attempt++)
        {
            var existing = await _tenantStore.FindByIdentifierAsync(candidate, cancellationToken);
            if (existing is null)
            {
                return candidate;
            }

            candidate = $"{slug}-{attempt}";
        }
    }
}
