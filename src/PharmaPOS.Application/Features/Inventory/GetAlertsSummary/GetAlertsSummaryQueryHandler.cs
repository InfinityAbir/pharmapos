using MediatR;
using PharmaPOS.Application.Common.Inventory;

namespace PharmaPOS.Application.Features.Inventory.GetAlertsSummary;

public sealed class GetAlertsSummaryQueryHandler : IRequestHandler<GetAlertsSummaryQuery, AlertsSummaryDto>
{
    private readonly IStockReader _stock;

    public GetAlertsSummaryQueryHandler(IStockReader stock) => _stock = stock;

    public Task<AlertsSummaryDto> Handle(
        GetAlertsSummaryQuery request,
        CancellationToken cancellationToken)
        => _stock.GetAlertsSummaryAsync(cancellationToken);
}