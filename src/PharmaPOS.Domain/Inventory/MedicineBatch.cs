using PharmaPOS.Domain.Catalog;

namespace PharmaPOS.Domain.Inventory;

/// <summary>
/// A batch (lot) of one medicine: the unit at which quantities and expiry are tracked.
/// Tenant-owned; the batch number is unique per medicine within a tenant. Quantities are
/// integers (pharmacy retail units are countable) and only change together with a ledger row —
/// never by direct edit.
/// </summary>
public sealed class MedicineBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Managed by Finbuckle; never set from client input.</summary>
    public string TenantId { get; set; } = default!;

    public Guid MedicineId { get; set; }

    public Medicine Medicine { get; set; } = null!;

    public string BatchNumber { get; set; } = default!;

    public decimal PurchasePrice { get; set; }

    public decimal SellingPrice { get; set; }

    public int Quantity { get; set; }

    public DateOnly? ManufactureDate { get; set; }

    public DateOnly ExpiryDate { get; set; }

    /// <summary>Free-text supplier until the supplier entity arrives (Phase 4).</summary>
    public string? SupplierName { get; set; }

    public string? PurchaseReference { get; set; }

    /// <summary>Optimistic-concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; set; }
}
