using MediatR;

namespace PharmaPOS.Application.Features.Catalog.UpdateManufacturer;

public sealed record UpdateManufacturerCommand(Guid Id, string Name, string? Country)
    : IRequest<ManufacturerDto>;