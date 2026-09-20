namespace PharmaPOS.Domain.Catalog;

/// <summary>
/// A medicine category (e.g. Antibiotic, Analgesic). Tenant-owned; the name is unique per tenant.
/// </summary>
public sealed class MedicineCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Managed by Finbuckle; never set from client input.</summary>
    public string TenantId { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public ICollection<Medicine> Medicines { get; set; } = [];
}
