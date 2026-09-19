namespace PharmaPOS.Application.Features.Auditing;

public sealed record AuditLogDto(
    Guid Id,
    string? ActorSubject,
    string Action,
    string? EntityType,
    string? EntityId,
    DateTimeOffset OccurredAtUtc);

/// <summary>
/// Reads audit entries for the current tenant. Implemented in Infrastructure; Finbuckle's query
/// filter guarantees the reader can only ever see the current tenant's rows.
/// </summary>
public interface IAuditLogReader
{
    Task<IReadOnlyList<AuditLogDto>> GetRecentAsync(int take, CancellationToken cancellationToken);
}
