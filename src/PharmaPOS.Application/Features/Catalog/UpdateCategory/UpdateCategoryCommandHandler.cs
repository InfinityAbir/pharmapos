using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Catalog;

namespace PharmaPOS.Application.Features.Catalog.UpdateCategory;

public sealed class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, CategoryDto>
{
    private readonly ICategoryRepository _categories;
    private readonly IAuditLogger _auditLogger;

    public UpdateCategoryCommandHandler(ICategoryRepository categories, IAuditLogger auditLogger)
    {
        _categories = categories;
        _auditLogger = auditLogger;
    }

    public async Task<CategoryDto> Handle(
        UpdateCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var category = await _categories.UpdateAsync(
            request.Id, request.Name, request.Description, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "category.updated",
                EntityType: "MedicineCategory",
                EntityId: category.Id.ToString(),
                Data: new Dictionary<string, string?> { ["name"] = category.Name }),
            cancellationToken);

        return category;
    }
}