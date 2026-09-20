namespace PharmaPOS.Domain.Identity;

/// <summary>
/// Join between a <see cref="TenantMembership"/> and a <see cref="Role"/> within the same tenant.
/// Tenant-owned; the tenant-composite FKs require the role and the membership to share a tenant.
/// </summary>
public sealed class MembershipRole
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Managed by Finbuckle; equals the membership's (and role's) tenant.</summary>
    public string TenantId { get; set; } = default!;

    public Guid MembershipId { get; set; }

    public TenantMembership Membership { get; set; } = null!;

    public Guid RoleId { get; set; }

    public Role Role { get; set; } = null!;
}