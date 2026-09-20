namespace PharmaPOS.Domain.Sales;

/// <summary>
/// Idempotency guard for returns: one key → one return. Replays return the stored return
/// without touching stock or money again (mirrors the checkout/purchase pattern).
/// </summary>
public sealed class SaleReturnIdempotencyKey
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Managed by Finbuckle; never set from client input.</summary>
    public string TenantId { get; set; } = default!;

    public string Key { get; set; } = default!;

    public Guid SaleReturnId { get; set; }

    public SaleReturn SaleReturn { get; set; } = null!;
}
