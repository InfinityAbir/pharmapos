using PharmaPOS.Application.Features.Catalog;

namespace PharmaPOS.Application.Common.Catalog;

/// <summary>
/// Tenant-scoped medicine catalog access. All operations are scoped to the current tenant by the
/// tenant-filtered context; barcode uniqueness and category/manufacturer references are enforced
/// per tenant.
/// </summary>
public interface IMedicineRepository
{
    Task<IReadOnlyList<MedicineDto>> SearchAsync(
        string? search,
        Guid? categoryId,
        Guid? manufacturerId,
        bool? isActive,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<MedicineDto?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<MedicineDto?> FindByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);

    Task<MedicineDto> CreateAsync(
        MedicineInput input,
        CancellationToken cancellationToken = default);

    Task<MedicineDto> UpdateAsync(
        Guid id,
        MedicineInput input,
        CancellationToken cancellationToken = default);

    Task<MedicineDto> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<int> CountActiveAsync(CancellationToken cancellationToken = default);
}

/// <summary>Validated medicine field set shared by create and update.</summary>
public sealed record MedicineInput(
    string BrandName,
    string GenericName,
    string Strength,
    string DosageForm,
    Guid? ManufacturerId,
    Guid? CategoryId,
    string? Barcode,
    string Unit,
    int? PackSize,
    decimal PurchasePrice,
    decimal SellingPrice,
    int ReorderLevel,
    decimal? VatRate,
    bool RequiresPrescription);
