using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Tenancy;
using PharmaPOS.Application.Features.Tenancy;

namespace PharmaPOS.Application.Features.Tenancy.CreateTenant;

public sealed class CreateTenantCommandHandler : IRequestHandler<CreateTenantCommand, TenantDto>
{
    private readonly IPlatformTenantStore _tenantStore;
    private readonly IAuditLogger _auditLogger;

    public CreateTenantCommandHandler(IPlatformTenantStore tenantStore, IAuditLogger auditLogger)
    {
        _tenantStore = tenantStore;
        _auditLogger = auditLogger;
    }

    public async Task<TenantDto> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = new TenantDto(
            Id: request.Identifier,
            Identifier: request.Identifier,
            Name: request.Name,
            Status: TenantStatus.Active,
            PlanCode: null);

        var created = await _tenantStore.CreateAsync(tenant, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "tenant.created",
                EntityType: "Tenant",
                EntityId: created.Identifier,
                Data: new Dictionary<string, string?> { ["name"] = created.Name }),
            cancellationToken);

        return created;
    }
}