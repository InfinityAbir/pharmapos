using MediatR;
using PharmaPOS.Application.Common.Inventory;

namespace PharmaPOS.Application.Features.Inventory.ListExpired;

public sealed class ListExpiredQueryHandler : IRequestHandler<ListExpiredQuery, IReadOnlyList<BatchDto>>
{
    private readonly IStockReader _stock;

    public ListExpiredQueryHandler(IStockReader stock) => _stock = stock;

    public Task<IReadOnlyList<BatchDto>> Handle(
        ListExpiredQuery request,
        CancellationToken cancellationToken)
        => _stock.ListExpiredAsync(cancellationToken);
}