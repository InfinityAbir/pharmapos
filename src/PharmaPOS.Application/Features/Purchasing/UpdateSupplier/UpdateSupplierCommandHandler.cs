using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Purchasing;

namespace PharmaPOS.Application.Features.Purchasing.UpdateSupplier;

public sealed class UpdateSupplierCommandHandler : IRequestHandler<UpdateSupplierCommand, SupplierDto>
{
    private readonly ISupplierRepository _suppliers;
    private readonly IAuditLogger _auditLogger;

    public UpdateSupplierCommandHandler(ISupplierRepository suppliers, IAuditLogger auditLogger)
    {
        _suppliers = suppliers;
        _auditLogger = auditLogger;
    }

    public async Task<SupplierDto> Handle(
        UpdateSupplierCommand request,
        CancellationToken cancellationToken)
    {
        var supplier = await _suppliers.UpdateAsync(request.Id, request.Input, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "supplier.updated",
                EntityType: "Supplier",
                EntityId: supplier.Id.ToString(),
                Data: new Dictionary<string, string?> { ["name"] = supplier.Name }),
            cancellationToken);

        return supplier;
    }
}