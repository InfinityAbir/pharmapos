using MediatR;

namespace PharmaPOS.Application.Features.Inventory.ListLowStock;

public sealed record ListLowStockQuery : IRequest<IReadOnlyList<MedicineStockDto>>;