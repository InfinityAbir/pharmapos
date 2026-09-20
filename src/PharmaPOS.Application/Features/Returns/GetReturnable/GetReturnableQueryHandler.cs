using MediatR;
using PharmaPOS.Application.Common.Sales;
using PharmaPOS.Domain.Exceptions;

namespace PharmaPOS.Application.Features.Returns.GetReturnable;

public sealed class GetReturnableQueryHandler : IRequestHandler<GetReturnableQuery, ReturnableSaleDto>
{
    private readonly IReturnReader _returns;

    public GetReturnableQueryHandler(IReturnReader returns) => _returns = returns;

    public async Task<ReturnableSaleDto> Handle(
        GetReturnableQuery request,
        CancellationToken cancellationToken)
    {
        var result = await _returns.GetReturnableAsync(request.SaleId, cancellationToken);
        if (result is null)
        {
            throw new NotFoundException("sale.not_found", "Sale not found in this tenant.");
        }

        return result;
    }
}
