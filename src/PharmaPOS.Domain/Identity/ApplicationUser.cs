namespace PharmaPOS.Domain.Identity;

/// <summary>
/// A human in the platform, identified by a Keycloak identity. Platform-owned (not tenant-scoped):
/// one row per person regardless of how many tenants they belong to. Sharing of a person across
/// tenants is expressed only through <see cref="TenantMembership"/> records.
/// </summary>
public sealed class ApplicationUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Keycloak <c>sub</c> claim. Unique; nullable only while a user is being provisioned.</summary>
    public string? KeycloakSubject { get; set; }

    /// <summary>Login email, always stored lower-case.</summary>
    public string Email { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}