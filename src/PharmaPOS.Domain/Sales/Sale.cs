namespace PharmaPOS.Domain.Sales;

/// <summary>
/// A completed sale: immutable financial record. Tenant-owned with a per-tenant sequential
/// invoice number. Money totals are computed server-side at completion; the client sends
/// quantities, never money.
/// </summary>
public sealed class Sale
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Managed by Finbuckle; never set from client input.</summary>
    public string TenantId { get; set; } = default!;

    /// <summary>Human invoice number (<c>INV-000001</c>…), unique per tenant.</summary>
    public string InvoiceNumber { get; set; } = default!;

    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public decimal Subtotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal PaidAmount { get; set; }

    /// <summary>Optional customer snapshot (no customer entity until Phase 6).</summary>
    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    /// <summary>Optional link to the customer record (Phase 6). Snapshot fields stay authoritative for receipts.</summary>
    public Guid? CustomerId { get; set; }

    /// <summary>Optional prescription reference (business link only, never clinical content).</summary>
    public string? PrescriptionReference { get; set; }

    /// <summary>Server-resolved cashier (sub or username), never from client input.</summary>
    public string? CashierSubject { get; set; }

    public ICollection<SaleItem> Items { get; set; } = [];

    public ICollection<SalePayment> Payments { get; set; } = [];

    /// <summary>Optimistic-concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; set; }
}
