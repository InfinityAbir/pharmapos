namespace PharmaPOS.Domain.Sales;

/// <summary>A single tender within a sale (split payments supported). Tenant-owned.</summary>
public sealed class SalePayment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Managed by Finbuckle; never set from client input.</summary>
    public string TenantId { get; set; } = default!;

    public Guid SaleId { get; set; }

    public Sale Sale { get; set; } = null!;

    public string Method { get; set; } = default!;

    public decimal Amount { get; set; }

    /// <summary>Transaction reference (required except cash).</summary>
    public string? Reference { get; set; }

    /// <summary>Server-resolved receiver (sub or username), never from client input.</summary>
    public string? ReceivedBy { get; set; }
}
