namespace PharmaPOS.Application.Common.Auditing;

/// <summary>A single auditable action. Metadata must never contain secrets or card data.</summary>
public sealed record AuditEntry(
    string Action,
    string? EntityType = null,
    string? EntityId = null,
    IReadOnlyDictionary<string, string?>? Data = null);

/// <summary>
/// Records security- and business-relevant actions. Implemented in Infrastructure so the
/// application layer stays free of persistence concerns.
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
