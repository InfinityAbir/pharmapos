using PharmaPOS.Domain.Catalog;

namespace PharmaPOS.Domain.Inventory;

/// <summary>
/// One row of the append-only inventory ledger. Every stock change writes exactly one movement
/// in the same transaction as the batch update; movements are never updated or deleted.
/// </summary>
public sealed class InventoryMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Managed by Finbuckle; never set from client input.</summary>
    public string TenantId { get; set; } = default!;

    public Guid MedicineId { get; set; }

    public Medicine Medicine { get; set; } = null!;

    public Guid BatchId { get; set; }

    public MedicineBatch Batch { get; set; } = null!;

    public string Type { get; set; } = default!;

    /// <summary>Signed quantity delta (negative for outflows).</summary>
    public int QuantityChange { get; set; }

    /// <summary>Batch quantity immediately after the change.</summary>
    public int QuantityAfter { get; set; }

    public string? Reason { get; set; }

    /// <summary>Server-resolved actor (sub or username), never from client input.</summary>
    public string? ActorSubject { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
