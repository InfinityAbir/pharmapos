using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Purchasing;

namespace PharmaPOS.Application.Features.Purchasing.UpdateDraft;

public sealed class UpdateDraftCommandHandler : IRequestHandler<UpdateDraftCommand, PurchaseDto>
{
    private readonly IPurchaseRepository _purchases;
    private readonly IAuditLogger _auditLogger;

    public UpdateDraftCommandHandler(IPurchaseRepository purchases, IAuditLogger auditLogger)
    {
        _purchases = purchases;
        _auditLogger = auditLogger;
    }

    public async Task<PurchaseDto> Handle(
        UpdateDraftCommand request,
        CancellationToken cancellationToken)
    {
        var purchase = await _purchases.UpdateDraftAsync(request.Id, request.Input, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "purchase.updated",
                EntityType: "Purchase",
                EntityId: purchase.Id.ToString(),
                Data: new Dictionary<string, string?>
                {
                    ["total"] = purchase.TotalAmount.ToString("F4"),
                }),
            cancellationToken);

        return purchase;
    }
}