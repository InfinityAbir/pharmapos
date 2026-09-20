namespace PharmaPOS.Domain.Identity;

/// <summary>
/// A named set of permissions within one tenant. Tenant-owned; the name is unique per tenant.
/// System roles are seeded and cannot be deleted or renamed.
/// </summary>
public sealed class Role
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Managed by Finbuckle; never set from client input.</summary>
    public string TenantId { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public bool IsSystem { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = [];

    public ICollection<MembershipRole> MembershipRoles { get; set; } = [];
}