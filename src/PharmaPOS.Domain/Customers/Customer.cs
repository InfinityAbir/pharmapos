namespace PharmaPOS.Domain.Customers;

/// <summary>
/// A customer the tenant sells to. Customer info is optional for cash sales; linking a sale
/// to a customer enables purchase history. Tenant-owned; phone is unique per tenant when set.
/// </summary>
public sealed class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Managed by Finbuckle; never set from client input.</summary>
    public string TenantId { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}
