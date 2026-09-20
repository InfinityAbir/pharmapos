using MediatR;

namespace PharmaPOS.Application.Features.Inventory.ListExpiring;

public sealed record ListExpiringQuery(int WithinDays = 30) : IRequest<IReadOnlyList<BatchDto>>;