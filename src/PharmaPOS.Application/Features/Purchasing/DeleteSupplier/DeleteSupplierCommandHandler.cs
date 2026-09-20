using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Purchasing;

namespace PharmaPOS.Application.Features.Purchasing.DeleteSupplier;

public sealed class DeleteSupplierCommandHandler : IRequestHandler<DeleteSupplierCommand, Unit>
{
    private readonly ISupplierRepository _suppliers;
    private readonly IAuditLogger _auditLogger;

    public DeleteSupplierCommandHandler(ISupplierRepository suppliers, IAuditLogger auditLogger)
    {
        _suppliers = suppliers;
        _auditLogger = auditLogger;
    }

    public async Task<Unit> Handle(
        DeleteSupplierCommand request,
        CancellationToken cancellationToken)
    {
        await _suppliers.DeleteAsync(request.Id, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "supplier.deleted",
                EntityType: "Supplier",
                EntityId: request.Id.ToString()),
            cancellationToken);

        return Unit.Value;
    }
}