using MediatR;

namespace PharmaPOS.Application.Features.Auditing.GetAuditLogs;

/// <summary>Returns recent audit entries for the current tenant. Requires the audit-read permission.</summary>
public sealed record GetAuditLogsQuery(int Take = 50) : IRequest<IReadOnlyList<AuditLogDto>>;
