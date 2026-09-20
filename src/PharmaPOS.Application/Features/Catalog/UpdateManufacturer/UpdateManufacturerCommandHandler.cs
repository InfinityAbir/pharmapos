using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Catalog;

namespace PharmaPOS.Application.Features.Catalog.UpdateManufacturer;

public sealed class UpdateManufacturerCommandHandler
    : IRequestHandler<UpdateManufacturerCommand, ManufacturerDto>
{
    private readonly IManufacturerRepository _manufacturers;
    private readonly IAuditLogger _auditLogger;

    public UpdateManufacturerCommandHandler(
        IManufacturerRepository manufacturers,
        IAuditLogger auditLogger)
    {
        _manufacturers = manufacturers;
        _auditLogger = auditLogger;
    }

    public async Task<ManufacturerDto> Handle(
        UpdateManufacturerCommand request,
        CancellationToken cancellationToken)
    {
        var manufacturer = await _manufacturers.UpdateAsync(
            request.Id, request.Name, request.Country, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "manufacturer.updated",
                EntityType: "Manufacturer",
                EntityId: manufacturer.Id.ToString(),
                Data: new Dictionary<string, string?> { ["name"] = manufacturer.Name }),
            cancellationToken);

        return manufacturer;
    }
}