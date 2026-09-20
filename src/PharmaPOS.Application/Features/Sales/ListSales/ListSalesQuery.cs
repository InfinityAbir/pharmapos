using MediatR;

namespace PharmaPOS.Application.Features.Sales.ListSales;

public sealed record ListSalesQuery(
    DateOnly? From,
    DateOnly? To,
    int Skip = 0,
    int Take = 25) : IRequest<IReadOnlyList<SaleDto>>;