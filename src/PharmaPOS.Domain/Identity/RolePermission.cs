namespace PharmaPOS.Domain.Identity;

/// <summary>
/// Join between a tenant <see cref="Domain.Identity.Role"/> and a platform <see cref="Permission"/>.
/// Tenant-owned; the tenant-composite FK makes it impossible to attach a role from another tenant.
/// </summary>
public sealed class RolePermission
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Managed by Finbuckle; equals the role's tenant.</summary>
    public string TenantId { get; set; } = default!;

    public Guid RoleId { get; set; }

    public Role Role { get; set; } = null!;

    public Guid PermissionId { get; set; }

    public Permission Permission { get; set; } = null!;
}