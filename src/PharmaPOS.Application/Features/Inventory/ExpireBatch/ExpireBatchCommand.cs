using MediatR;

namespace PharmaPOS.Application.Features.Inventory.ExpireBatch;

public sealed record ExpireBatchCommand(Guid Id) : IRequest<BatchDto>;