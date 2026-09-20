using MediatR;
using PharmaPOS.Application.Common.Authorization;
using PharmaPOS.Application.Common.Tenancy;
using PharmaPOS.Application.Features.Tenancy;
using PharmaPOS.Domain.Exceptions;

namespace PharmaPOS.Application.Features.Identity.GetCurrentMembership;

public sealed class GetCurrentMembershipQueryHandler : IRequestHandler<GetCurrentMembershipQuery, CurrentMembershipDto>
{
    private readonly IMembershipResolver _membershipResolver;
    private readonly IPlatformTenantStore _tenantStore;

    public GetCurrentMembershipQueryHandler(
        IMembershipResolver membershipResolver,
        IPlatformTenantStore tenantStore)
    {
        _membershipResolver = membershipResolver;
        _tenantStore = tenantStore;
    }

    public async Task<CurrentMembershipDto> Handle(
        GetCurrentMembershipQuery request,
        CancellationToken cancellationToken)
    {
        var identity = string.IsNullOrWhiteSpace(request.Username)
            ? request.Subject
            : request.Username;

        var access = await _membershipResolver.ResolveAccessAsync(identity, cancellationToken);
        if (access is null)
        {
            throw new ForbiddenException(
                "membership.not_found",
                $"No active membership exists for the caller in tenant '{request.TenantIdentifier}'.");
        }

        var tenant = await _tenantStore.FindByIdentifierAsync(request.TenantIdentifier, cancellationToken);

        return new CurrentMembershipDto(
            Subject: request.Subject,
            Username: request.Username,
            Email: access.Email,
            TenantIdentifier: request.TenantIdentifier,
            TenantName: tenant?.Name ?? request.TenantIdentifier,
            UserStatus: MembershipStatusLabel.Active,
            MembershipStatus: MembershipStatusLabel.Active,
            IsDefaultMembership: access.IsDefault,
            Roles: access.Roles,
            Permissions: access.Permissions);
    }

    private static class MembershipStatusLabel
    {
        public const string Active = "Active";
    }
}