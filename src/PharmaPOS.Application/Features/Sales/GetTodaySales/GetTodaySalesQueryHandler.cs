using MediatR;
using PharmaPOS.Application.Common.Sales;

namespace PharmaPOS.Application.Features.Sales.GetTodaySales;

public sealed class GetTodaySalesQueryHandler : IRequestHandler<GetTodaySalesQuery, TodaySalesDto>
{
    private readonly ISaleReader _sales;

    public GetTodaySalesQueryHandler(ISaleReader sales) => _sales = sales;

    public Task<TodaySalesDto> Handle(
        GetTodaySalesQuery request,
        CancellationToken cancellationToken)
        => _sales.GetTodaySummaryAsync(cancellationToken);
}