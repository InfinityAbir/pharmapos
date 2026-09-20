using MediatR;

namespace PharmaPOS.Application.Features.Catalog.ListManufacturers;

public sealed record ListManufacturersQuery : IRequest<IReadOnlyList<ManufacturerDto>>;