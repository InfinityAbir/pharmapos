using MediatR;

namespace PharmaPOS.Application.Features.Catalog.CreateCategory;

public sealed record CreateCategoryCommand(string Name, string? Description) : IRequest<CategoryDto>;