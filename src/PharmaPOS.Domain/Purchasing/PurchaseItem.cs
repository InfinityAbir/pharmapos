using PharmaPOS.Domain.Catalog;

namespace PharmaPOS.Domain.Purchasing;

/// <summary>
/// One line of a purchase: medicine + batch + quantity + pricing. Tenant-owned with
/// tenant-composite FKs so cross-tenant references are structurally impossible.
/// </summary>
public sealed class PurchaseItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Managed by Finbuckle; never set from client input.</summary>
    public string TenantId { get; set; } = default!;

    public Guid PurchaseId { get; set; }

    public Purchase Purchase { get; set; } = null!;

    public Guid MedicineId { get; set; }

    public Medicine Medicine { get; set; } = null!;

    public string BatchNumber { get; set; } = default!;

    public DateOnly? ManufactureDate { get; set; }

    public DateOnly ExpiryDate { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    /// <summary>Retail price to set on receipt; null keeps the existing/catalog price.</summary>
    public decimal? SellingPrice { get; set; }
}
