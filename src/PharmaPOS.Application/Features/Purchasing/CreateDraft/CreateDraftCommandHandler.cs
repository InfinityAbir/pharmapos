using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Purchasing;

namespace PharmaPOS.Application.Features.Purchasing.CreateDraft;

public sealed class CreateDraftCommandHandler : IRequestHandler<CreateDraftCommand, PurchaseDto>
{
    private readonly IPurchaseRepository _purchases;
    private readonly IAuditLogger _auditLogger;

    public CreateDraftCommandHandler(IPurchaseRepository purchases, IAuditLogger auditLogger)
    {
        _purchases = purchases;
        _auditLogger = auditLogger;
    }

    public async Task<PurchaseDto> Handle(
        CreateDraftCommand request,
        CancellationToken cancellationToken)
    {
        var purchase = await _purchases.CreateDraftAsync(request.Input, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "purchase.created",
                EntityType: "Purchase",
                EntityId: purchase.Id.ToString(),
                Data: new Dictionary<string, string?>
                {
                    ["supplier"] = purchase.SupplierName,
                    ["total"] = purchase.TotalAmount.ToString("F4"),
                }),
            cancellationToken);

        return purchase;
    }
}