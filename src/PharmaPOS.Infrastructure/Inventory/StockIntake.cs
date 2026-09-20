using Microsoft.EntityFrameworkCore;
using PharmaPOS.Domain.Catalog;
using PharmaPOS.Domain.Exceptions;
using PharmaPOS.Domain.Inventory;
using PharmaPOS.Infrastructure.Persistence;

namespace PharmaPOS.Infrastructure.Inventory;

/// <summary>
/// Shared batch upsert for stock intake. Used by manual receipt and purchase receiving so both
/// paths enforce identical rules: match by (medicine, batch number), top-up dates must match,
/// prices follow the latest explicit values. Makes no save call — the caller owns the unit of
/// work (purchase receiving must post the whole document atomically).
/// </summary>
internal static class StockIntake
{
    public static async Task<MedicineBatch> ResolveForReceiveAsync(
        AppDbContext db,
        Medicine medicine,
        string batchNumber,
        decimal? purchasePrice,
        decimal? sellingPrice,
        DateOnly? manufactureDate,
        DateOnly expiryDate,
        string? supplierName,
        string? purchaseReference,
        CancellationToken cancellationToken)
    {
        batchNumber = batchNumber.Trim();

        var batch = await db.MedicineBatches
            .FirstOrDefaultAsync(
                candidate => candidate.MedicineId == medicine.Id
                    && candidate.BatchNumber == batchNumber,
                cancellationToken);

        if (batch is null)
        {
            batch = new MedicineBatch
            {
                TenantId = TenantScope.CurrentTenantId(db),
                MedicineId = medicine.Id,
                Medicine = medicine,
                BatchNumber = batchNumber,
                PurchasePrice = purchasePrice ?? medicine.PurchasePrice,
                SellingPrice = sellingPrice ?? medicine.SellingPrice,
                Quantity = 0,
                ManufactureDate = manufactureDate,
                ExpiryDate = expiryDate,
                SupplierName = Normalize(supplierName),
                PurchaseReference = Normalize(purchaseReference),
            };

            db.MedicineBatches.Add(batch);
            return batch;
        }

        // Topping up an existing lot: dates must match (a mismatch signals the wrong
        // batch number); prices follow the latest explicit values.
        if ((manufactureDate.HasValue && manufactureDate != batch.ManufactureDate)
            || expiryDate != batch.ExpiryDate)
        {
            throw new BusinessRuleException(
                "batch.date_mismatch",
                $"Batch '{batchNumber}' already exists with different dates. " +
                "Receive into a new batch number instead.");
        }

        if (purchasePrice.HasValue)
        {
            batch.PurchasePrice = purchasePrice.Value;
        }

        if (sellingPrice.HasValue)
        {
            batch.SellingPrice = sellingPrice.Value;
        }

        return batch;
    }

    public static InventoryMovement BuildMovement(
        string tenantId,
        Guid medicineId,
        MedicineBatch batch,
        string type,
        int quantityChange,
        int quantityAfter,
        string? reason,
        string? actor)
        => new()
        {
            TenantId = tenantId,
            MedicineId = medicineId,
            BatchId = batch.Id,
            Type = type,
            QuantityChange = quantityChange,
            QuantityAfter = quantityAfter,
            Reason = Normalize(reason),
            ActorSubject = actor,
            OccurredAtUtc = DateTimeOffset.UtcNow,
        };

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
