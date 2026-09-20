namespace PharmaPOS.Domain.Purchasing;

/// <summary>
/// A supplier the tenant buys from. Tenant-owned; the name is unique per tenant.
/// </summary>
public sealed class Supplier
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Managed by Finbuckle; never set from client input.</summary>
    public string TenantId { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Company { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? ContactPerson { get; set; }

    public string? PaymentTerms { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Purchase> Purchases { get; set; } = [];
}
