using MediatR;
using PharmaPOS.Application.Common.Sales;
using PharmaPOS.Domain.Exceptions;

namespace PharmaPOS.Application.Features.Sales.GetSale;

public sealed class GetSaleQueryHandler : IRequestHandler<GetSaleQuery, SaleDto>
{
    private readonly ISaleReader _sales;

    public GetSaleQueryHandler(ISaleReader sales) => _sales = sales;

    public async Task<SaleDto> Handle(
        GetSaleQuery request,
        CancellationToken cancellationToken)
    {
        var sale = await _sales.FindAsync(request.Id, cancellationToken);
        if (sale is null)
        {
            throw new NotFoundException("sale.not_found", $"Sale '{request.Id}' was not found.");
        }

        return sale;
    }
}