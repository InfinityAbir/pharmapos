using FluentValidation;
using MediatR;
using PharmaPOS.Application.Common.Inventory;

namespace PharmaPOS.Application.Features.Inventory.ListExpiring;

public sealed class ListExpiringQueryValidator : AbstractValidator<ListExpiringQuery>
{
    public ListExpiringQueryValidator()
    {
        RuleFor(query => query.WithinDays).InclusiveBetween(1, 365);
    }
}

public sealed class ListExpiringQueryHandler : IRequestHandler<ListExpiringQuery, IReadOnlyList<BatchDto>>
{
    private readonly IStockReader _stock;

    public ListExpiringQueryHandler(IStockReader stock) => _stock = stock;

    public Task<IReadOnlyList<BatchDto>> Handle(
        ListExpiringQuery request,
        CancellationToken cancellationToken)
        => _stock.ListExpiringAsync(request.WithinDays, cancellationToken);
}