using FluentValidation;
using MediatR;
using PharmaPOS.Application.Common.Inventory;

namespace PharmaPOS.Application.Features.Inventory.ListMovements;

public sealed class ListMovementsQueryValidator : AbstractValidator<ListMovementsQuery>
{
    public ListMovementsQueryValidator()
    {
        RuleFor(query => query.Skip).GreaterThanOrEqualTo(0);
        RuleFor(query => query.Take).InclusiveBetween(1, 100);
    }
}

public sealed class ListMovementsQueryHandler : IRequestHandler<ListMovementsQuery, IReadOnlyList<MovementDto>>
{
    private readonly IMovementReader _movements;

    public ListMovementsQueryHandler(IMovementReader movements) => _movements = movements;

    public Task<IReadOnlyList<MovementDto>> Handle(
        ListMovementsQuery request,
        CancellationToken cancellationToken)
        => _movements.ListAsync(
            request.MedicineId,
            request.BatchId,
            request.Skip,
            request.Take,
            cancellationToken);
}