using FluentValidation;
using MediatR;
using PharmaPOS.Application.Common.Purchasing;

namespace PharmaPOS.Application.Features.Purchasing.ListPurchases;

public sealed class ListPurchasesQueryValidator : AbstractValidator<ListPurchasesQuery>
{
    public ListPurchasesQueryValidator()
    {
        RuleFor(query => query.Skip).GreaterThanOrEqualTo(0);
        RuleFor(query => query.Take).InclusiveBetween(1, 100);
    }
}

public sealed class ListPurchasesQueryHandler : IRequestHandler<ListPurchasesQuery, IReadOnlyList<PurchaseDto>>
{
    private readonly IPurchaseRepository _purchases;

    public ListPurchasesQueryHandler(IPurchaseRepository purchases) => _purchases = purchases;

    public Task<IReadOnlyList<PurchaseDto>> Handle(
        ListPurchasesQuery request,
        CancellationToken cancellationToken)
        => _purchases.ListAsync(
            request.SupplierId,
            request.Status,
            request.Skip,
            request.Take,
            cancellationToken);
}