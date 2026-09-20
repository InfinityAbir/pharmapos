using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Purchasing;

namespace PharmaPOS.Application.Features.Purchasing.CreateSupplier;

public sealed class CreateSupplierCommandHandler : IRequestHandler<CreateSupplierCommand, SupplierDto>
{
    private readonly ISupplierRepository _suppliers;
    private readonly IAuditLogger _auditLogger;

    public CreateSupplierCommandHandler(ISupplierRepository suppliers, IAuditLogger auditLogger)
    {
        _suppliers = suppliers;
        _auditLogger = auditLogger;
    }

    public async Task<SupplierDto> Handle(
        CreateSupplierCommand request,
        CancellationToken cancellationToken)
    {
        var supplier = await _suppliers.CreateAsync(request.Input, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "supplier.created",
                EntityType: "Supplier",
                EntityId: supplier.Id.ToString(),
                Data: new Dictionary<string, string?> { ["name"] = supplier.Name }),
            cancellationToken);

        return supplier;
    }
}