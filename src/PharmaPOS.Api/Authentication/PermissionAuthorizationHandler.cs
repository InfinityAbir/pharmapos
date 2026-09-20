using Microsoft.AspNetCore.Authorization;
using PharmaPOS.Application.Common.Authorization;

namespace PharmaPOS.Api.Authentication;

/// <summary>
/// Grants access when the database membership graph grants the required permission to the caller
/// for the current tenant.
///
/// The token proves <i>who</i> the caller is (<c>sub</c> / <c>preferred_username</c>) and <i>which
/// tenant</i> is active (<c>tenant_id</c>, consumed by Finbuckle before this handler runs).
/// <i>What</i> the caller may do is resolved server-side from
/// <c>ApplicationUser → TenantMembership → Role → Permission</c> via
/// <see cref="IMembershipResolver"/>. Callers without an active membership in the current tenant
/// resolve to <c>null</c> and are denied. Registered as scoped: the resolver needs the
/// request-scoped tenant-filtered context.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IMembershipResolver _membershipResolver;

    public PermissionAuthorizationHandler(IMembershipResolver membershipResolver)
        => _membershipResolver = membershipResolver;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var subject = context.User.FindFirst("sub")?.Value;
        var username = context.User.FindFirst("preferred_username")?.Value;
        var identity = !string.IsNullOrWhiteSpace(subject) ? subject : username;

        if (string.IsNullOrWhiteSpace(identity))
        {
            return;
        }

        var access = await _membershipResolver.ResolveAccessAsync(identity);

        if (access is not null && access.Permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
