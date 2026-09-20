namespace PharmaPOS.Domain.Sales;

/// <summary>
/// Per-tenant invoice counter. Incremented by atomic SQL inside the sale transaction so
/// concurrent checkouts can never issue duplicate invoice numbers.
/// </summary>
public sealed class InvoiceCounter
{
    /// <summary>Managed by Finbuckle; the counter row key.</summary>
    public string TenantId { get; set; } = default!;

    public long LastNumber { get; set; }
}
