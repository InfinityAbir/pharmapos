using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Inventory;

namespace PharmaPOS.Application.Features.Inventory.ExpireBatch;

public sealed class ExpireBatchCommandHandler : IRequestHandler<ExpireBatchCommand, BatchDto>
{
    private readonly IBatchRepository _batches;
    private readonly IAuditLogger _auditLogger;

    public ExpireBatchCommandHandler(IBatchRepository batches, IAuditLogger auditLogger)
    {
        _batches = batches;
        _auditLogger = auditLogger;
    }

    public async Task<BatchDto> Handle(
        ExpireBatchCommand request,
        CancellationToken cancellationToken)
    {
        var before = await _batches.FindAsync(request.Id, cancellationToken);
        var batch = await _batches.ExpireAsync(request.Id, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "batch.expired",
                EntityType: "MedicineBatch",
                EntityId: batch.Id.ToString(),
                Data: new Dictionary<string, string?>
                {
                    ["medicine"] = batch.MedicineName,
                    ["batch"] = batch.BatchNumber,
                    ["quantity_removed"] = (before?.Quantity ?? batch.Quantity).ToString(),
                }),
            cancellationToken);

        return batch;
    }
}