using MediatR;

namespace PharmaPOS.Application.Features.Catalog.CreateManufacturer;

public sealed record CreateManufacturerCommand(string Name, string? Country) : IRequest<ManufacturerDto>;