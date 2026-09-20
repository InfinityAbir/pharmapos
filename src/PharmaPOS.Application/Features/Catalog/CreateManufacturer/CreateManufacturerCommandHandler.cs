using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Catalog;

namespace PharmaPOS.Application.Features.Catalog.CreateManufacturer;

public sealed class CreateManufacturerCommandHandler
    : IRequestHandler<CreateManufacturerCommand, ManufacturerDto>
{
    private readonly IManufacturerRepository _manufacturers;
    private readonly IAuditLogger _auditLogger;

    public CreateManufacturerCommandHandler(
        IManufacturerRepository manufacturers,
        IAuditLogger auditLogger)
    {
        _manufacturers = manufacturers;
        _auditLogger = auditLogger;
    }

    public async Task<ManufacturerDto> Handle(
        CreateManufacturerCommand request,
        CancellationToken cancellationToken)
    {
        var manufacturer = await _manufacturers.CreateAsync(
            request.Name, request.Country, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "manufacturer.created",
                EntityType: "Manufacturer",
                EntityId: manufacturer.Id.ToString(),
                Data: new Dictionary<string, string?> { ["name"] = manufacturer.Name }),
            cancellationToken);

        return manufacturer;
    }
}