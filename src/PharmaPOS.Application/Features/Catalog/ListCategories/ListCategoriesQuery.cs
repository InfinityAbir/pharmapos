using MediatR;

namespace PharmaPOS.Application.Features.Catalog.ListCategories;

public sealed record ListCategoriesQuery : IRequest<IReadOnlyList<CategoryDto>>;