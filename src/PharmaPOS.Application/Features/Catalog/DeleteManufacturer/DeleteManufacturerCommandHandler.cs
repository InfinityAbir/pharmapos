using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Catalog;

namespace PharmaPOS.Application.Features.Catalog.DeleteManufacturer;

public sealed class DeleteManufacturerCommandHandler : IRequestHandler<DeleteManufacturerCommand, Unit>
{
    private readonly IManufacturerRepository _manufacturers;
    private readonly IAuditLogger _auditLogger;

    public DeleteManufacturerCommandHandler(
        IManufacturerRepository manufacturers,
        IAuditLogger auditLogger)
    {
        _manufacturers = manufacturers;
        _auditLogger = auditLogger;
    }

    public async Task<Unit> Handle(
        DeleteManufacturerCommand request,
        CancellationToken cancellationToken)
    {
        await _manufacturers.DeleteAsync(request.Id, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "manufacturer.deleted",
                EntityType: "Manufacturer",
                EntityId: request.Id.ToString()),
            cancellationToken);

        return Unit.Value;
    }
}