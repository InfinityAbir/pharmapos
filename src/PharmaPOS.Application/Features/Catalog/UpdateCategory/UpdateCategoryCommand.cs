using MediatR;

namespace PharmaPOS.Application.Features.Catalog.UpdateCategory;

public sealed record UpdateCategoryCommand(Guid Id, string Name, string? Description) : IRequest<CategoryDto>;