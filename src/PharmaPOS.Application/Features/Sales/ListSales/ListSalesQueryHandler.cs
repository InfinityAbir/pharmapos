using FluentValidation;
using MediatR;
using PharmaPOS.Application.Common.Sales;

namespace PharmaPOS.Application.Features.Sales.ListSales;

public sealed class ListSalesQueryValidator : AbstractValidator<ListSalesQuery>
{
    public ListSalesQueryValidator()
    {
        RuleFor(query => query.Skip).GreaterThanOrEqualTo(0);
        RuleFor(query => query.Take).InclusiveBetween(1, 100);
    }
}

public sealed class ListSalesQueryHandler : IRequestHandler<ListSalesQuery, IReadOnlyList<SaleDto>>
{
    private readonly ISaleReader _sales;

    public ListSalesQueryHandler(ISaleReader sales) => _sales = sales;

    public Task<IReadOnlyList<SaleDto>> Handle(
        ListSalesQuery request,
        CancellationToken cancellationToken)
        => _sales.ListAsync(
            request.From,
            request.To,
            request.Skip,
            request.Take,
            cancellationToken);
}