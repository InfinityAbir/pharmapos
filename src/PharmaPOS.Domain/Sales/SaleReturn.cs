namespace PharmaPOS.Domain.Sales;

/// <summary>
/// A sale return: authorized reversal of one or more sold lines. The original <see cref="Sale"/>
/// is immutable — returns record the reversal, restock (when applicable) and the refund.
/// Refund money is computed server-side from the original line nets; the client sends
/// quantities, never money.
/// </summary>
public sealed class SaleReturn
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Managed by Finbuckle; never set from client input.</summary>
    public string TenantId { get; set; } = default!;

    public Guid SaleId { get; set; }

    public Sale Sale { get; set; } = null!;

    /// <summary>Human reason (damaged, expired, wrong item, customer request, …).</summary>
    public string Reason { get; set; } = default!;

    /// <summary>When true, returned units go back to the original batches with SALE_RETURN movements.</summary>
    public bool Restock { get; set; } = true;

    /// <summary>Refund method (Cash/Card/bKash/Nagad/Other — same catalog as payments).</summary>
    public string RefundMethod { get; set; } = default!;

    public string? RefundReference { get; set; }

    /// <summary>Server-computed pro-rata refund from the original line nets.</summary>
    public decimal RefundAmount { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Server-resolved actor (sub or username), never from client input.</summary>
    public string? ActorSubject { get; set; }

    public ICollection<SaleReturnItem> Items { get; set; } = [];
}
