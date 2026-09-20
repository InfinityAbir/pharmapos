using MediatR;
using PharmaPOS.Application.Common.Catalog;

namespace PharmaPOS.Application.Features.Catalog.SearchMedicines;

public sealed class SearchMedicinesQueryHandler : IRequestHandler<SearchMedicinesQuery, IReadOnlyList<MedicineDto>>
{
    private readonly IMedicineRepository _medicines;

    public SearchMedicinesQueryHandler(IMedicineRepository medicines) => _medicines = medicines;

    public Task<IReadOnlyList<MedicineDto>> Handle(
        SearchMedicinesQuery request,
        CancellationToken cancellationToken)
        => _medicines.SearchAsync(
            request.Search,
            request.CategoryId,
            request.ManufacturerId,
            request.IsActive,
            request.Skip,
            request.Take,
            cancellationToken);
}