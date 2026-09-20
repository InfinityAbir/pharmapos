using PharmaPOS.Domain.Catalog;
using PharmaPOS.Domain.Inventory;

namespace PharmaPOS.Domain.Sales;

/// <summary>
/// One sold line against one batch. FEFO allocation may split a cart line across batches, so a
/// sale carries one row per (line × batch). Tenant-owned with tenant-composite FKs.
/// </summary>
public sealed class SaleItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Managed by Finbuckle; never set from client input.</summary>
    public string TenantId { get; set; } = default!;

    public Guid SaleId { get; set; }

    public Sale Sale { get; set; } = null!;

    public Guid MedicineId { get; set; }

    public Medicine Medicine { get; set; } = null!;

    public Guid BatchId { get; set; }

    public MedicineBatch Batch { get; set; } = null!;

    public int Quantity { get; set; }

    /// <summary>Resolved server-side from the batch selling price at completion.</summary>
    public decimal UnitPrice { get; set; }

    public decimal DiscountAmount { get; set; }
}
