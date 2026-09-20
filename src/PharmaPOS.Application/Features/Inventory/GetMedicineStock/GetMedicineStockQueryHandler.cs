using MediatR;
using PharmaPOS.Application.Common.Inventory;
using PharmaPOS.Domain.Exceptions;

namespace PharmaPOS.Application.Features.Inventory.GetMedicineStock;

public sealed class GetMedicineStockQueryHandler : IRequestHandler<GetMedicineStockQuery, MedicineStockDto>
{
    private readonly IStockReader _stock;

    public GetMedicineStockQueryHandler(IStockReader stock) => _stock = stock;

    public async Task<MedicineStockDto> Handle(
        GetMedicineStockQuery request,
        CancellationToken cancellationToken)
    {
        var stock = await _stock.GetMedicineStockAsync(request.MedicineId, cancellationToken);
        if (stock is null)
        {
            throw new NotFoundException(
                "medicine.not_found",
                $"Medicine '{request.MedicineId}' was not found.");
        }

        return stock;
    }
}