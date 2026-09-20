namespace PharmaPOS.Application.Features.Catalog;

public sealed record CatalogSummaryDto(
    int MedicineCount,
    int ActiveMedicineCount,
    int CategoryCount,
    int ManufacturerCount);
