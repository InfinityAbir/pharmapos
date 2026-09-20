using MediatR;

namespace PharmaPOS.Application.Features.Inventory.ListStock;

public sealed record ListStockQuery(string? Search, bool LowStockOnly = false)
    : IRequest<IReadOnlyList<MedicineStockDto>>;