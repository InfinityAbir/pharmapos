using MediatR;

namespace PharmaPOS.Application.Features.Catalog.GetMedicineByBarcode;

public sealed record GetMedicineByBarcodeQuery(string Barcode) : IRequest<MedicineDto>;