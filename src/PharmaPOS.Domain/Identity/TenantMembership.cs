namespace PharmaPOS.Domain.Identity;

/// <summary>Lifecycle status of a membership or tenant, kept as stable strings (readable, indexable).</summary>
public static class MembershipStatus
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";
}

/// <summary>
/// Binds an <see cref="ApplicationUser"/> to a tenant. Tenant-owned: Finbuckle scopes reads and
/// writes to the current tenant, and the unique key <c>(TenantId, ApplicationUserId)</c> guarantees
/// a user appears at most once per tenant.
/// </summary>
public sealed class TenantMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Managed by Finbuckle; never set from client input.</summary>
    public string TenantId { get; set; } = default!;

    public Guid ApplicationUserId { get; set; }

    public ApplicationUser ApplicationUser { get; set; } = null!;

    public string Status { get; set; } = MembershipStatus.Active;

    public bool IsDefault { get; set; }

    public DateTimeOffset JoinedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<MembershipRole> MembershipRoles { get; set; } = [];

    public bool IsActive => Status == MembershipStatus.Active;
}