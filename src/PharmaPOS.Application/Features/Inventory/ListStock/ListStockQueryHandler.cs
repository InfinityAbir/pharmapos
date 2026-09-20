using MediatR;
using PharmaPOS.Application.Common.Inventory;

namespace PharmaPOS.Application.Features.Inventory.ListStock;

public sealed class ListStockQueryHandler : IRequestHandler<ListStockQuery, IReadOnlyList<MedicineStockDto>>
{
    private readonly IStockReader _stock;

    public ListStockQueryHandler(IStockReader stock) => _stock = stock;

    public Task<IReadOnlyList<MedicineStockDto>> Handle(
        ListStockQuery request,
        CancellationToken cancellationToken)
        => _stock.ListStockAsync(request.Search, request.LowStockOnly, cancellationToken);
}