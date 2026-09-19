using Microsoft.AspNetCore.Authorization;

namespace PharmaPOS.Api.Authentication;

/// <summary>
/// Grants access when the authenticated principal carries the required <c>permissions</c> claim.
///
/// Phase 0 reads permissions from the Keycloak-issued, signature-validated token. Phase 1 adds
/// tenant-scoped permissions resolved from <c>TenantMembership</c> in the database, at which point
/// this handler will consult that source (or run alongside a membership-based handler). Client
/// claims are never trusted directly from the request — only from the validated token.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    public const string PermissionClaimType = "permissions";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.HasClaim(PermissionClaimType, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
