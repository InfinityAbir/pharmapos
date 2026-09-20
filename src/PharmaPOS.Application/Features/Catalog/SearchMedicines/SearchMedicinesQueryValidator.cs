using FluentValidation;

namespace PharmaPOS.Application.Features.Catalog.SearchMedicines;

public sealed class SearchMedicinesQueryValidator : AbstractValidator<SearchMedicinesQuery>
{
    public SearchMedicinesQueryValidator()
    {
        RuleFor(query => query.Skip).GreaterThanOrEqualTo(0);
        RuleFor(query => query.Take).InclusiveBetween(1, 100);
        RuleFor(query => query.Search).MaximumLength(128);
    }
}