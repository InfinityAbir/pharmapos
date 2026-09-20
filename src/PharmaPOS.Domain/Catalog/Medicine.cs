namespace PharmaPOS.Domain.Catalog;

/// <summary>
/// A medicine in the tenant's catalog: the sellable product definition (brand/generic, strength,
/// form, pricing). Tenant-owned; the barcode is unique per tenant when present.
///
/// Stock quantities and batches live in the inventory module (Phase 3) — this entity carries no
/// quantities. Prices here are the catalog reference until batch pricing arrives.
/// Medicines are never hard-deleted (future sales reference them); they are deactivated instead.
/// </summary>
public sealed class Medicine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Managed by Finbuckle; never set from client input.</summary>
    public string TenantId { get; set; } = default!;

    public string BrandName { get; set; } = default!;

    public string GenericName { get; set; } = default!;

    public string Strength { get; set; } = default!;

    public string DosageForm { get; set; } = default!;

    public Guid? ManufacturerId { get; set; }

    public Manufacturer? Manufacturer { get; set; }

    public Guid? CategoryId { get; set; }

    public MedicineCategory? Category { get; set; }

    /// <summary>Scanner barcode; optional, unique per tenant when present.</summary>
    public string? Barcode { get; set; }

    /// <summary>Selling unit, e.g. piece, strip, bottle, box, tube.</summary>
    public string Unit { get; set; } = default!;

    /// <summary>Units per pack (e.g. tablets per strip); null when not applicable.</summary>
    public int? PackSize { get; set; }

    public decimal PurchasePrice { get; set; }

    public decimal SellingPrice { get; set; }

    public int ReorderLevel { get; set; }

    /// <summary>VAT rate in percent; null means no VAT configured.</summary>
    public decimal? VatRate { get; set; }

    public bool RequiresPrescription { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Optimistic-concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; set; }
}
