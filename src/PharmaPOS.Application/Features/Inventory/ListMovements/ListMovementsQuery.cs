using MediatR;

namespace PharmaPOS.Application.Features.Inventory.ListMovements;

public sealed record ListMovementsQuery(
    Guid? MedicineId,
    Guid? BatchId,
    int Skip = 0,
    int Take = 50) : IRequest<IReadOnlyList<MovementDto>>;