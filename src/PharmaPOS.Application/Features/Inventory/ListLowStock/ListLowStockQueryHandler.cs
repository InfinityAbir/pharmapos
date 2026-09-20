using MediatR;
using PharmaPOS.Application.Common.Inventory;

namespace PharmaPOS.Application.Features.Inventory.ListLowStock;

public sealed class ListLowStockQueryHandler : IRequestHandler<ListLowStockQuery, IReadOnlyList<MedicineStockDto>>
{
    private readonly IStockReader _stock;

    public ListLowStockQueryHandler(IStockReader stock) => _stock = stock;

    public Task<IReadOnlyList<MedicineStockDto>> Handle(
        ListLowStockQuery request,
        CancellationToken cancellationToken)
        => _stock.ListLowStockAsync(cancellationToken);
}