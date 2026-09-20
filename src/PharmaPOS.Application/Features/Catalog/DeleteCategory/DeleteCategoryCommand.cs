using MediatR;

namespace PharmaPOS.Application.Features.Catalog.DeleteCategory;

public sealed record DeleteCategoryCommand(Guid Id) : IRequest<Unit>;