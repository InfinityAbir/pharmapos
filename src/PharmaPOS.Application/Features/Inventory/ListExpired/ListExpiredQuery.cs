using MediatR;

namespace PharmaPOS.Application.Features.Inventory.ListExpired;

public sealed record ListExpiredQuery : IRequest<IReadOnlyList<BatchDto>>;