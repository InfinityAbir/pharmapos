using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Tenancy;
using PharmaPOS.Application.Features.Tenancy;
using PharmaPOS.Domain.Exceptions;

namespace PharmaPOS.Application.Features.Tenancy.SetTenantStatus;

public sealed class SetTenantStatusCommandHandler : IRequestHandler<SetTenantStatusCommand, TenantDto>
{
    private readonly IPlatformTenantStore _tenantStore;
    private readonly IAuditLogger _auditLogger;

    public SetTenantStatusCommandHandler(IPlatformTenantStore tenantStore, IAuditLogger auditLogger)
    {
        _tenantStore = tenantStore;
        _auditLogger = auditLogger;
    }

    public async Task<TenantDto> Handle(SetTenantStatusCommand request, CancellationToken cancellationToken)
    {
        var existing = await _tenantStore.FindByIdentifierAsync(request.Identifier, cancellationToken);
        if (existing is null)
        {
            throw new NotFoundException("tenant.not_found", $"Tenant '{request.Identifier}' was not found.");
        }

        var updated = await _tenantStore.UpdateAsync(
            existing with { Status = request.Status },
            cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "tenant.status.changed",
                EntityType: "Tenant",
                EntityId: updated.Identifier,
                Data: new Dictionary<string, string?> { ["status"] = updated.Status }),
            cancellationToken);

        return updated;
    }
}