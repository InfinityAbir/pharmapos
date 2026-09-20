using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Catalog;

namespace PharmaPOS.Application.Features.Catalog.DeleteCategory;

public sealed class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand, Unit>
{
    private readonly ICategoryRepository _categories;
    private readonly IAuditLogger _auditLogger;

    public DeleteCategoryCommandHandler(ICategoryRepository categories, IAuditLogger auditLogger)
    {
        _categories = categories;
        _auditLogger = auditLogger;
    }

    public async Task<Unit> Handle(
        DeleteCategoryCommand request,
        CancellationToken cancellationToken)
    {
        await _categories.DeleteAsync(request.Id, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "category.deleted",
                EntityType: "MedicineCategory",
                EntityId: request.Id.ToString()),
            cancellationToken);

        return Unit.Value;
    }
}