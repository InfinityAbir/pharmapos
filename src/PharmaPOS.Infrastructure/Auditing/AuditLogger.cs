using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Domain.Auditing;
using PharmaPOS.Infrastructure.Persistence;

namespace PharmaPOS.Infrastructure.Auditing;

/// <summary>
/// Persists audit entries through EF Core. Tenant and actor are taken from server-side context
/// (Finbuckle tenant + the authenticated principal), never from the request body.
/// </summary>
public sealed class AuditLogger : IAuditLogger
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogger(AppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        var auditLog = new AuditLog
        {
            Action = entry.Action,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            Data = entry.Data is null ? null : JsonSerializer.Serialize(entry.Data, SerializerOptions),
            ActorSubject = httpContext?.User.FindFirst("sub")?.Value
                ?? httpContext?.User.FindFirst("preferred_username")?.Value,
            IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
            CorrelationId = httpContext?.TraceIdentifier,
            OccurredAtUtc = DateTimeOffset.UtcNow,
        };

        // TenantId is assigned by Finbuckle.MultiTenant on save.
        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
