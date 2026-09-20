using FluentValidation;

namespace PharmaPOS.Application.Features.Inventory.ListBatches;

public sealed class ListBatchesQueryValidator : AbstractValidator<ListBatchesQuery>
{
    public ListBatchesQueryValidator()
    {
        RuleFor(query => query.ExpiringWithinDays)
            .InclusiveBetween(1, 365)
            .When(query => query.ExpiringWithinDays.HasValue);
    }
}