using MediatR;
using PharmaPOS.Application.Common.Catalog;
using PharmaPOS.Domain.Exceptions;

namespace PharmaPOS.Application.Features.Catalog.GetMedicineByBarcode;

public sealed class GetMedicineByBarcodeQueryHandler : IRequestHandler<GetMedicineByBarcodeQuery, MedicineDto>
{
    private readonly IMedicineRepository _medicines;

    public GetMedicineByBarcodeQueryHandler(IMedicineRepository medicines) => _medicines = medicines;

    public async Task<MedicineDto> Handle(
        GetMedicineByBarcodeQuery request,
        CancellationToken cancellationToken)
    {
        var medicine = await _medicines.FindByBarcodeAsync(request.Barcode, cancellationToken);
        if (medicine is null)
        {
            throw new NotFoundException(
                "medicine.not_found",
                $"No medicine with barcode '{request.Barcode}' exists in this tenant.");
        }

        return medicine;
    }
}