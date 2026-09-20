namespace PharmaPOS.Domain.Sales;

/// <summary>
/// Idempotency record for sale completion. A repeated key returns the stored sale instead of
/// charging/posting again.
/// </summary>
public sealed class SaleIdempotencyKey
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Managed by Finbuckle; never set from client input.</summary>
    public string TenantId { get; set; } = default!;

    public string Key { get; set; } = default!;

    public Guid SaleId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
