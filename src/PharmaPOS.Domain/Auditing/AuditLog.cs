namespace PharmaPOS.Domain.Auditing;

/// <summary>
/// Append-only audit record for security- and business-relevant actions.
///
/// Tenant-owned: Finbuckle.MultiTenant manages <see cref="TenantId"/> and ensures records are
/// isolated per tenant. Platform-level (non-tenant) auditing will use a separate mechanism when
/// the platform administration module lands. The application never updates or deletes audit rows.
/// </summary>
public sealed class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Managed by Finbuckle; never set from client input.</summary>
    public string TenantId { get; set; } = default!;

    /// <summary>Keycloak subject (or application user id) that performed the action, if any.</summary>
    public string? ActorSubject { get; set; }

    /// <summary>Stable action code, e.g. <c>tenant.resolved</c>, <c>sale.completed</c>.</summary>
    public string Action { get; set; } = default!;

    public string? EntityType { get; set; }

    public string? EntityId { get; set; }

    /// <summary>Free-form structured metadata serialized as JSON. Never contains secrets.</summary>
    public string? Data { get; set; }

    public string? IpAddress { get; set; }

    public string? CorrelationId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
