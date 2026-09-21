namespace PharmaPOS.Application.Common.Tenancy;

/// <summary>
/// Provisions the database side of a newly registered tenant owner: application user, owner
/// membership and the tenant permission catalog. Runs outside any request tenant context (self
/// registration is anonymous), binding explicitly to the new tenant — the same pattern the
/// database initializer uses for the demo tenant.
/// </summary>
public interface ITenantProvisioning
{
    Task ProvisionOwnerAsync(
        string tenantIdentifier,
        string email,
        string displayName,
        string? keycloakSubject,
        CancellationToken cancellationToken = default);
}
