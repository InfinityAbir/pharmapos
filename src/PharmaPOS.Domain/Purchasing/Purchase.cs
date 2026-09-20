namespace PharmaPOS.Domain.Purchasing;

/// <summary>
/// A purchase document: supplier + dated line items that post to inventory on receipt.
/// Tenant-owned. Money totals are computed server-side and stored for reporting; the client
/// never sends totals. Received and cancelled documents are immutable.
/// </summary>
public sealed class Purchase
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Managed by Finbuckle; never set from client input.</summary>
    public string TenantId { get; set; } = default!;

    public Guid SupplierId { get; set; }

    public Supplier Supplier { get; set; } = null!;

    public DateOnly PurchaseDate { get; set; }

    public string Status { get; set; } = PurchaseStatus.Draft;

    /// <summary>The supplier's invoice number, for matching paperwork.</summary>
    public string? SupplierInvoiceNumber { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ReceivedAtUtc { get; set; }

    public ICollection<PurchaseItem> Items { get; set; } = [];

    /// <summary>Optimistic-concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; set; }
}
