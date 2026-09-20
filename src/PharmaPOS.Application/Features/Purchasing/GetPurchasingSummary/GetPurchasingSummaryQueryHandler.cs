using MediatR;
using PharmaPOS.Application.Common.Purchasing;

namespace PharmaPOS.Application.Features.Purchasing.GetPurchasingSummary;

public sealed class GetPurchasingSummaryQueryHandler
    : IRequestHandler<GetPurchasingSummaryQuery, PurchasingSummaryDto>
{
    private readonly IPurchaseRepository _purchases;

    public GetPurchasingSummaryQueryHandler(IPurchaseRepository purchases) => _purchases = purchases;

    public Task<PurchasingSummaryDto> Handle(
        GetPurchasingSummaryQuery request,
        CancellationToken cancellationToken)
        => _purchases.GetSummaryAsync(cancellationToken);
}