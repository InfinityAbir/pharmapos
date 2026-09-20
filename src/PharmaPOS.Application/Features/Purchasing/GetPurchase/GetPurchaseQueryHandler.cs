using MediatR;
using PharmaPOS.Application.Common.Purchasing;
using PharmaPOS.Domain.Exceptions;

namespace PharmaPOS.Application.Features.Purchasing.GetPurchase;

public sealed class GetPurchaseQueryHandler : IRequestHandler<GetPurchaseQuery, PurchaseDto>
{
    private readonly IPurchaseRepository _purchases;

    public GetPurchaseQueryHandler(IPurchaseRepository purchases) => _purchases = purchases;

    public async Task<PurchaseDto> Handle(
        GetPurchaseQuery request,
        CancellationToken cancellationToken)
    {
        var purchase = await _purchases.FindAsync(request.Id, cancellationToken);
        if (purchase is null)
        {
            throw new NotFoundException("purchase.not_found", $"Purchase '{request.Id}' was not found.");
        }

        return purchase;
    }
}