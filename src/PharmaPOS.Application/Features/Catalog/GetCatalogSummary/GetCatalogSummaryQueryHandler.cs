using MediatR;
using PharmaPOS.Application.Common.Catalog;

namespace PharmaPOS.Application.Features.Catalog.GetCatalogSummary;

public sealed class GetCatalogSummaryQueryHandler : IRequestHandler<GetCatalogSummaryQuery, CatalogSummaryDto>
{
    private readonly IMedicineRepository _medicines;
    private readonly ICategoryRepository _categories;
    private readonly IManufacturerRepository _manufacturers;

    public GetCatalogSummaryQueryHandler(
        IMedicineRepository medicines,
        ICategoryRepository categories,
        IManufacturerRepository manufacturers)
    {
        _medicines = medicines;
        _categories = categories;
        _manufacturers = manufacturers;
    }

    public async Task<CatalogSummaryDto> Handle(
        GetCatalogSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var total = await _medicines.CountAsync(cancellationToken);
        var active = await _medicines.CountActiveAsync(cancellationToken);
        var categories = await _categories.ListAsync(cancellationToken);
        var manufacturers = await _manufacturers.ListAsync(cancellationToken);

        return new CatalogSummaryDto(
            MedicineCount: total,
            ActiveMedicineCount: active,
            CategoryCount: categories.Count,
            ManufacturerCount: manufacturers.Count);
    }
}