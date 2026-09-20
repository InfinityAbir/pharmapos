using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Catalog;

namespace PharmaPOS.Application.Features.Catalog.CreateCategory;

public sealed class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, CategoryDto>
{
    private readonly ICategoryRepository _categories;
    private readonly IAuditLogger _auditLogger;

    public CreateCategoryCommandHandler(ICategoryRepository categories, IAuditLogger auditLogger)
    {
        _categories = categories;
        _auditLogger = auditLogger;
    }

    public async Task<CategoryDto> Handle(
        CreateCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var category = await _categories.CreateAsync(request.Name, request.Description, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "category.created",
                EntityType: "MedicineCategory",
                EntityId: category.Id.ToString(),
                Data: new Dictionary<string, string?> { ["name"] = category.Name }),
            cancellationToken);

        return category;
    }
}