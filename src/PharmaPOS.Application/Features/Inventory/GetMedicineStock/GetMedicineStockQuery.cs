using MediatR;

namespace PharmaPOS.Application.Features.Inventory.GetMedicineStock;

public sealed record GetMedicineStockQuery(Guid MedicineId) : IRequest<MedicineStockDto>;