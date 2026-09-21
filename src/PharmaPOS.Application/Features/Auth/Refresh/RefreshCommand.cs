using FluentValidation;
using MediatR;
using PharmaPOS.Application.Common.Identity;

namespace PharmaPOS.Application.Features.Auth.Refresh;

public sealed record RefreshCommand(string RefreshToken) : IRequest<LoginResponse>;

public sealed class RefreshCommandValidator : AbstractValidator<RefreshCommand>
{
    public RefreshCommandValidator()
    {
        RuleFor(command => command.RefreshToken).NotEmpty();
    }
}

public sealed class RefreshCommandHandler : IRequestHandler<RefreshCommand, LoginResponse>
{
    private readonly IKeycloakClient _keycloak;

    public RefreshCommandHandler(IKeycloakClient keycloak) => _keycloak = keycloak;

    public async Task<LoginResponse> Handle(RefreshCommand request, CancellationToken cancellationToken)
    {
        var tokens = await _keycloak.RefreshTokenAsync(request.RefreshToken, cancellationToken);

        return new LoginResponse(
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.ExpiresInSeconds);
    }
}
