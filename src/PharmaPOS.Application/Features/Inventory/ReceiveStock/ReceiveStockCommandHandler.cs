using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Inventory;

namespace PharmaPOS.Application.Features.Inventory.ReceiveStock;

public sealed class ReceiveStockCommandHandler : IRequestHandler<ReceiveStockCommand, BatchDto>
{
    private readonly IBatchRepository _batches;
    private readonly IAuditLogger _auditLogger;

    public ReceiveStockCommandHandler(IBatchRepository batches, IAuditLogger auditLogger)
    {
        _batches = batches;
        _auditLogger = auditLogger;
    }

    public async Task<BatchDto> Handle(
        ReceiveStockCommand request,
        CancellationToken cancellationToken)
    {
        var batch = await _batches.ReceiveAsync(request.Input, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "stock.received",
                EntityType: "MedicineBatch",
                EntityId: batch.Id.ToString(),
                Data: new Dictionary<string, string?>
                {
                    ["medicine"] = batch.MedicineName,
                    ["batch"] = batch.BatchNumber,
                    ["quantity"] = request.Input.Quantity.ToString(),
                    ["quantity_after"] = batch.Quantity.ToString(),
                }),
            cancellationToken);

        return batch;
    }
}