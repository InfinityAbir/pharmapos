using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Inventory;

namespace PharmaPOS.Application.Features.Inventory.AdjustStock;

public sealed class AdjustStockCommandHandler : IRequestHandler<AdjustStockCommand, BatchDto>
{
    private readonly IBatchRepository _batches;
    private readonly IAuditLogger _auditLogger;

    public AdjustStockCommandHandler(IBatchRepository batches, IAuditLogger auditLogger)
    {
        _batches = batches;
        _auditLogger = auditLogger;
    }

    public async Task<BatchDto> Handle(
        AdjustStockCommand request,
        CancellationToken cancellationToken)
    {
        var batch = await _batches.AdjustAsync(
            request.Id, request.QuantityChange, request.Reason, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "stock.adjusted",
                EntityType: "MedicineBatch",
                EntityId: batch.Id.ToString(),
                Data: new Dictionary<string, string?>
                {
                    ["medicine"] = batch.MedicineName,
                    ["batch"] = batch.BatchNumber,
                    ["change"] = request.QuantityChange.ToString(),
                    ["quantity_after"] = batch.Quantity.ToString(),
                    ["reason"] = request.Reason,
                }),
            cancellationToken);

        return batch;
    }
}