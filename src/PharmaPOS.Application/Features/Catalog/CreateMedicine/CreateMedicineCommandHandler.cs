using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Catalog;

namespace PharmaPOS.Application.Features.Catalog.CreateMedicine;

public sealed class CreateMedicineCommandHandler : IRequestHandler<CreateMedicineCommand, MedicineDto>
{
    private readonly IMedicineRepository _medicines;
    private readonly IAuditLogger _auditLogger;

    public CreateMedicineCommandHandler(IMedicineRepository medicines, IAuditLogger auditLogger)
    {
        _medicines = medicines;
        _auditLogger = auditLogger;
    }

    public async Task<MedicineDto> Handle(
        CreateMedicineCommand request,
        CancellationToken cancellationToken)
    {
        var medicine = await _medicines.CreateAsync(request.Input, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "medicine.created",
                EntityType: "Medicine",
                EntityId: medicine.Id.ToString(),
                Data: new Dictionary<string, string?>
                {
                    ["brand"] = medicine.BrandName,
                    ["generic"] = medicine.GenericName,
                    ["strength"] = medicine.Strength,
                }),
            cancellationToken);

        return medicine;
    }
}