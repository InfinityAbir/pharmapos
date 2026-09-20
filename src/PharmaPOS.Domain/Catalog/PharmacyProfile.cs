namespace PharmaPOS.Domain.Catalog;

/// <summary>
/// The tenant's pharmacy profile: receipt/shop settings. Tenant-owned with exactly one row per
/// tenant (<c>UNIQUE (TenantId)</c>). Logo upload is deferred — only a URL is stored.
/// </summary>
public sealed class PharmacyProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Managed by Finbuckle; never set from client input.</summary>
    public string TenantId { get; set; } = default!;

    public string PharmacyName { get; set; } = default!;

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    public string? RegistrationInfo { get; set; }

    public string? VatInfo { get; set; }

    public string? InvoiceFooter { get; set; }

    public string Currency { get; set; } = "BDT";

    public string DefaultLanguage { get; set; } = "en";

    public string Timezone { get; set; } = "Asia/Dhaka";

    public string? LogoUrl { get; set; }
}
