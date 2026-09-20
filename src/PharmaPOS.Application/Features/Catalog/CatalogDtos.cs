namespace PharmaPOS.Application.Features.Catalog;

public sealed record ManufacturerDto(
    Guid Id,
    string Name,
    string? Country);

public sealed record CategoryDto(
    Guid Id,
    string Name,
    string? Description);

public sealed record MedicineDto(
    Guid Id,
    string BrandName,
    string GenericName,
    string Strength,
    string DosageForm,
    Guid? ManufacturerId,
    string? ManufacturerName,
    Guid? CategoryId,
    string? CategoryName,
    string? Barcode,
    string Unit,
    int? PackSize,
    decimal PurchasePrice,
    decimal SellingPrice,
    int ReorderLevel,
    decimal? VatRate,
    bool RequiresPrescription,
    bool IsActive);

public sealed record PharmacyProfileDto(
    string PharmacyName,
    string? Address,
    string? Phone,
    string? Email,
    string? Website,
    string? RegistrationInfo,
    string? VatInfo,
    string? InvoiceFooter,
    string Currency,
    string DefaultLanguage,
    string Timezone,
    string? LogoUrl);
