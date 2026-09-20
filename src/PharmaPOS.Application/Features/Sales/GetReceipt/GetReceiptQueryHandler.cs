using MediatR;
using PharmaPOS.Application.Common.Sales;
using PharmaPOS.Domain.Exceptions;

namespace PharmaPOS.Application.Features.Sales.GetReceipt;

public sealed class GetReceiptQueryHandler : IRequestHandler<GetReceiptQuery, ReceiptDto>
{
    private readonly ISaleReader _sales;

    public GetReceiptQueryHandler(ISaleReader sales) => _sales = sales;

    public async Task<ReceiptDto> Handle(
        GetReceiptQuery request,
        CancellationToken cancellationToken)
    {
        var receipt = await _sales.GetReceiptAsync(request.Id, cancellationToken);
        if (receipt is null)
        {
            throw new NotFoundException("sale.not_found", $"Sale '{request.Id}' was not found.");
        }

        return receipt;
    }
}