using MediatR;

namespace PharmaPOS.Application.Features.Catalog.DeleteManufacturer;

public sealed record DeleteManufacturerCommand(Guid Id) : IRequest<Unit>;