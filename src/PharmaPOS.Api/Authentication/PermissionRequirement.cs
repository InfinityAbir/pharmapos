using Microsoft.AspNetCore.Authorization;

namespace PharmaPOS.Api.Authentication;

/// <summary>Requires a specific permission, resolved server-side.</summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission) => Permission = permission;

    public string Permission { get; }
}
