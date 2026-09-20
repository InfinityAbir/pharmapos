using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Purchasing;

namespace PharmaPOS.Application.Features.Purchasing.ReceivePurchase;

public sealed class ReceivePurchaseCommandHandler
    : IRequestHandler<ReceivePurchaseCommand, ReceivePurchaseResult>
{
    private readonly IPurchaseRepository _purchases;
    private readonly IAuditLogger _auditLogger;

    public ReceivePurchaseCommandHandler(IPurchaseRepository purchases, IAuditLogger auditLogger)
    {
        _purchases = purchases;
        _auditLogger = auditLogger;
    }

    public async Task<ReceivePurchaseResult> Handle(
        ReceivePurchaseCommand request,
        CancellationToken cancellationToken)
    {
        var result = await _purchases.ReceiveAsync(
            request.Id, request.IdempotencyKey, cancellationToken);

        // Replays return the stored outcome: auditing again would misrepresent history.
        if (!result.Replayed)
        {
            await _auditLogger.LogAsync(
                new AuditEntry(
                    Action: "purchase.received",
                    EntityType: "Purchase",
                    EntityId: result.Purchase.Id.ToString(),
                    Data: new Dictionary<string, string?>
                    {
                        ["supplier"] = result.Purchase.SupplierName,
                        ["items"] = result.Purchase.Items.Count.ToString(),
                        ["total"] = result.Purchase.TotalAmount.ToString("F4"),
                    }),
                cancellationToken);
        }

        return result;
    }
}