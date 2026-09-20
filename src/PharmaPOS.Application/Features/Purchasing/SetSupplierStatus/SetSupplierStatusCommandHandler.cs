using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Purchasing;

namespace PharmaPOS.Application.Features.Purchasing.SetSupplierStatus;

public sealed class SetSupplierStatusCommandHandler : IRequestHandler<SetSupplierStatusCommand, SupplierDto>
{
    private readonly ISupplierRepository _suppliers;
    private readonly IAuditLogger _auditLogger;

    public SetSupplierStatusCommandHandler(ISupplierRepository suppliers, IAuditLogger auditLogger)
    {
        _suppliers = suppliers;
        _auditLogger = auditLogger;
    }

    public async Task<SupplierDto> Handle(
        SetSupplierStatusCommand request,
        CancellationToken cancellationToken)
    {
        var supplier = await _suppliers.SetActiveAsync(request.Id, request.IsActive, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "supplier.status.changed",
                EntityType: "Supplier",
                EntityId: supplier.Id.ToString(),
                Data: new Dictionary<string, string?> { ["active"] = supplier.IsActive.ToString() }),
            cancellationToken);

        return supplier;
    }
}