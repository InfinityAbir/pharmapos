using MediatR;
using PharmaPOS.Application.Common.Catalog;

namespace PharmaPOS.Application.Features.Catalog.ListCategories;

public sealed class ListCategoriesQueryHandler : IRequestHandler<ListCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    private readonly ICategoryRepository _categories;

    public ListCategoriesQueryHandler(ICategoryRepository categories) => _categories = categories;

    public Task<IReadOnlyList<CategoryDto>> Handle(
        ListCategoriesQuery request,
        CancellationToken cancellationToken)
        => _categories.ListAsync(cancellationToken);
}