using MediatR;
using PharmaPOS.Application.Common.Catalog;

namespace PharmaPOS.Application.Features.Catalog.ListManufacturers;

public sealed class ListManufacturersQueryHandler
    : IRequestHandler<ListManufacturersQuery, IReadOnlyList<ManufacturerDto>>
{
    private readonly IManufacturerRepository _manufacturers;

    public ListManufacturersQueryHandler(IManufacturerRepository manufacturers)
        => _manufacturers = manufacturers;

    public Task<IReadOnlyList<ManufacturerDto>> Handle(
        ListManufacturersQuery request,
        CancellationToken cancellationToken)
        => _manufacturers.ListAsync(cancellationToken);
}