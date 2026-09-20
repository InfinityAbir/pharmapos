using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Catalog;

namespace PharmaPOS.Application.Features.Catalog.SetMedicineStatus;

public sealed class SetMedicineStatusCommandHandler : IRequestHandler<SetMedicineStatusCommand, MedicineDto>
{
    private readonly IMedicineRepository _medicines;
    private readonly IAuditLogger _auditLogger;

    public SetMedicineStatusCommandHandler(IMedicineRepository medicines, IAuditLogger auditLogger)
    {
        _medicines = medicines;
        _auditLogger = auditLogger;
    }

    public async Task<MedicineDto> Handle(
        SetMedicineStatusCommand request,
        CancellationToken cancellationToken)
    {
        var medicine = await _medicines.SetActiveAsync(request.Id, request.IsActive, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "medicine.status.changed",
                EntityType: "Medicine",
                EntityId: medicine.Id.ToString(),
                Data: new Dictionary<string, string?> { ["active"] = medicine.IsActive.ToString() }),
            cancellationToken);

        return medicine;
    }
}