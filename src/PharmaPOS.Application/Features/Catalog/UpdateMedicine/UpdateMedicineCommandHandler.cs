using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Catalog;

namespace PharmaPOS.Application.Features.Catalog.UpdateMedicine;

public sealed class UpdateMedicineCommandHandler : IRequestHandler<UpdateMedicineCommand, MedicineDto>
{
    private readonly IMedicineRepository _medicines;
    private readonly IAuditLogger _auditLogger;

    public UpdateMedicineCommandHandler(IMedicineRepository medicines, IAuditLogger auditLogger)
    {
        _medicines = medicines;
        _auditLogger = auditLogger;
    }

    public async Task<MedicineDto> Handle(
        UpdateMedicineCommand request,
        CancellationToken cancellationToken)
    {
        var before = await _medicines.FindByIdAsync(request.Id, cancellationToken);
        var medicine = await _medicines.UpdateAsync(request.Id, request.Input, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "medicine.updated",
                EntityType: "Medicine",
                EntityId: medicine.Id.ToString(),
                Data: new Dictionary<string, string?> { ["brand"] = medicine.BrandName }),
            cancellationToken);

        if (before is not null
            && (before.PurchasePrice != medicine.PurchasePrice
                || before.SellingPrice != medicine.SellingPrice))
        {
            await _auditLogger.LogAsync(
                new AuditEntry(
                    Action: "medicine.price.changed",
                    EntityType: "Medicine",
                    EntityId: medicine.Id.ToString(),
                    Data: new Dictionary<string, string?>
                    {
                        ["purchase_before"] = before.PurchasePrice.ToString("F4"),
                        ["purchase_after"] = medicine.PurchasePrice.ToString("F4"),
                        ["selling_before"] = before.SellingPrice.ToString("F4"),
                        ["selling_after"] = medicine.SellingPrice.ToString("F4"),
                    }),
                cancellationToken);
        }

        return medicine;
    }
}