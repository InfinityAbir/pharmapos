namespace PharmaPOS.Domain.Purchasing;

/// <summary>
/// Idempotency record for critical commands (purchase receiving first; sale completion and
/// returns reuse it). A repeated key returns the stored purchase instead of re-executing.
/// </summary>
public sealed class IdempotencyKey
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Managed by Finbuckle; never set from client input.</summary>
    public string TenantId { get; set; } = default!;

    public string Key { get; set; } = default!;

    public Guid PurchaseId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
