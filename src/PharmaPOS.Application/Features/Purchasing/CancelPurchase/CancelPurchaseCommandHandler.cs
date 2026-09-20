using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Purchasing;

namespace PharmaPOS.Application.Features.Purchasing.CancelPurchase;

public sealed class CancelPurchaseCommandHandler : IRequestHandler<CancelPurchaseCommand, PurchaseDto>
{
    private readonly IPurchaseRepository _purchases;
    private readonly IAuditLogger _auditLogger;

    public CancelPurchaseCommandHandler(IPurchaseRepository purchases, IAuditLogger auditLogger)
    {
        _purchases = purchases;
        _auditLogger = auditLogger;
    }

    public async Task<PurchaseDto> Handle(
        CancelPurchaseCommand request,
        CancellationToken cancellationToken)
    {
        var purchase = await _purchases.CancelAsync(request.Id, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "purchase.cancelled",
                EntityType: "Purchase",
                EntityId: purchase.Id.ToString()),
            cancellationToken);

        return purchase;
    }
}