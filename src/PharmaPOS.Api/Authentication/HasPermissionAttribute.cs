using Microsoft.AspNetCore.Authorization;

namespace PharmaPOS.Api.Authentication;

/// <summary>
/// Declarative guard: <c>[HasPermission(Permissions.Platform.AuditRead)]</c>. Translates to a
/// <c>perm:</c> policy handled by <see cref="PermissionPolicyProvider"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
        => Policy = $"{PermissionPolicyProvider.Prefix}{permission}";
}
