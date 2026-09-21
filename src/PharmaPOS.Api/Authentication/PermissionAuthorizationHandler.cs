using Microsoft.AspNetCore.Authorization;
using PharmaPOS.Application.Common.Authorization;

namespace PharmaPOS.Api.Authentication;

/// <summary>
/// Grants access when the caller's Keycloak realm roles grant the required permission.
///
/// The token proves <i>who</i> the caller is (<c>sub</c> / <c>preferred_username</c>), <i>which
/// tenant</i> is active (<c>tenant_id</c>, consumed by Finbuckle before this handler runs) and
/// <i>what</i> the caller may do (<c>realm_access</c> roles, mapped to role claims at
/// authentication time). The mapping lives in <see cref="RealmRoles"/>; callers whose realm
/// roles grant none of the required roles are denied. Registered as a singleton: no
/// request-scoped membership lookup is needed anymore.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (!RealmRoles.RolePermissions.TryGetValue(requirement.Permission, out var allowedRoles))
        {
            return Task.CompletedTask;
        }

        if (allowedRoles.Any(context.User.IsInRole))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
