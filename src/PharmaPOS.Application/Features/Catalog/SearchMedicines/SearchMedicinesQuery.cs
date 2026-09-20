using MediatR;
using PharmaPOS.Application.Common.Catalog;

namespace PharmaPOS.Application.Features.Catalog.SearchMedicines;

public sealed record SearchMedicinesQuery(
    string? Search,
    Guid? CategoryId,
    Guid? ManufacturerId,
    bool? IsActive,
    int Skip = 0,
    int Take = 25) : IRequest<IReadOnlyList<MedicineDto>>;