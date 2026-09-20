using PharmaPOS.Domain.Catalog;
using PharmaPOS.Domain.Inventory;

namespace PharmaPOS.Domain.Sales;

/// <summary>
/// One returned line against the original <see cref="SaleItem"/>. Restocks target the original
/// batch (the batch the units were sold from). Tenant-owned with tenant-composite FKs.
/// </summary>
public sealed class SaleReturnItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Managed by Finbuckle; never set from client input.</summary>
    public string TenantId { get; set; } = default!;

    public Guid SaleReturnId { get; set; }

    public SaleReturn SaleReturn { get; set; } = null!;

    public Guid SaleItemId { get; set; }

    public SaleItem SaleItem { get; set; } = null!;

    public Guid MedicineId { get; set; }

    public Medicine Medicine { get; set; } = null!;

    public Guid BatchId { get; set; }

    public MedicineBatch Batch { get; set; } = null!;

    public int Quantity { get; set; }

    /// <summary>Snapshot of the original line unit price at return time.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Server-computed pro-rata share of the original line discount + refund portion.</summary>
    public decimal RefundAmount { get; set; }
}
