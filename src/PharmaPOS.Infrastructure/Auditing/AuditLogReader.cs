using Microsoft.EntityFrameworkCore;
using PharmaPOS.Application.Features.Auditing;
using PharmaPOS.Infrastructure.Persistence;

namespace PharmaPOS.Infrastructure.Auditing;

/// <summary>
/// Reads audit entries through the tenant-filtered context. Because <c>AuditLog</c> is
/// multi-tenant, the query filter restricts results to the current tenant even though no
/// <c>WHERE TenantId</c> appears here.
/// </summary>
public sealed class AuditLogReader : IAuditLogReader
{
    private readonly AppDbContext _dbContext;

    public AuditLogReader(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<AuditLogDto>> GetRecentAsync(
        int take,
        CancellationToken cancellationToken)
    {
        return await _dbContext.AuditLogs
            .AsNoTracking()
            .OrderByDescending(audit => audit.OccurredAtUtc)
            .Take(take)
            .Select(audit => new AuditLogDto(
                audit.Id,
                audit.ActorSubject,
                audit.Action,
                audit.EntityType,
                audit.EntityId,
                audit.OccurredAtUtc))
            .ToListAsync(cancellationToken);
    }
}
