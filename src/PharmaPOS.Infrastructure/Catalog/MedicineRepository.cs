using Microsoft.EntityFrameworkCore;
using PharmaPOS.Application.Common.Catalog;
using PharmaPOS.Application.Features.Catalog;
using PharmaPOS.Domain.Catalog;
using PharmaPOS.Domain.Exceptions;
using PharmaPOS.Infrastructure.Persistence;

namespace PharmaPOS.Infrastructure.Catalog;

/// <summary>
/// Tenant-scoped medicine catalog. Scoped to the current tenant by the filtered
/// <see cref="AppDbContext"/>; manufacturer/category references are validated against the same
/// tenant and barcodes are unique per tenant.
/// </summary>
public sealed class MedicineRepository : IMedicineRepository
{
    private readonly AppDbContext _dbContext;

    public MedicineRepository(AppDbContext dbContext)
        => _dbContext = dbContext;

    public async Task<IReadOnlyList<MedicineDto>> SearchAsync(
        string? search,
        Guid? categoryId,
        Guid? manufacturerId,
        bool? isActive,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Medicines
            .AsNoTracking()
            .Include(medicine => medicine.Manufacturer)
            .Include(medicine => medicine.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLike(search.Trim())}%";
            query = query.Where(medicine =>
                EF.Functions.ILike(medicine.BrandName, pattern)
                || EF.Functions.ILike(medicine.GenericName, pattern)
                || (medicine.Barcode != null && EF.Functions.ILike(medicine.Barcode, pattern))
                || (medicine.Manufacturer != null && EF.Functions.ILike(medicine.Manufacturer.Name, pattern)));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(medicine => medicine.CategoryId == categoryId.Value);
        }

        if (manufacturerId.HasValue)
        {
            query = query.Where(medicine => medicine.ManufacturerId == manufacturerId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(medicine => medicine.IsActive == isActive.Value);
        }

        var medicines = await query
            .OrderBy(medicine => medicine.BrandName)
            .ThenBy(medicine => medicine.Strength)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return medicines.Select(ToDto).ToArray();
    }

    public async Task<MedicineDto?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var medicine = await _dbContext.Medicines
            .AsNoTracking()
            .Include(candidate => candidate.Manufacturer)
            .Include(candidate => candidate.Category)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return medicine is null ? null : ToDto(medicine);
    }

    public async Task<MedicineDto?> FindByBarcodeAsync(string barcode, CancellationToken cancellationToken)
    {
        var normalized = NormalizeBarcode(barcode);
        if (normalized is null)
        {
            return null;
        }

        var medicine = await _dbContext.Medicines
            .AsNoTracking()
            .Include(candidate => candidate.Manufacturer)
            .Include(candidate => candidate.Category)
            .FirstOrDefaultAsync(candidate => candidate.Barcode == normalized, cancellationToken);

        return medicine is null ? null : ToDto(medicine);
    }

    public async Task<MedicineDto> CreateAsync(
        MedicineInput input,
        CancellationToken cancellationToken)
    {
        await EnsureReferencesExistAsync(input.ManufacturerId, input.CategoryId, cancellationToken);
        await EnsureBarcodeUniqueAsync(NormalizeBarcode(input.Barcode), excludeId: null, cancellationToken);

        var medicine = new Medicine
        {
            TenantId = TenantScope.CurrentTenantId(_dbContext),
            BrandName = input.BrandName.Trim(),
            GenericName = input.GenericName.Trim(),
            Strength = input.Strength.Trim(),
            DosageForm = input.DosageForm,
            ManufacturerId = input.ManufacturerId,
            CategoryId = input.CategoryId,
            Barcode = NormalizeBarcode(input.Barcode),
            Unit = input.Unit.Trim(),
            PackSize = input.PackSize,
            PurchasePrice = input.PurchasePrice,
            SellingPrice = input.SellingPrice,
            ReorderLevel = input.ReorderLevel,
            VatRate = input.VatRate,
            RequiresPrescription = input.RequiresPrescription,
            IsActive = true,
        };

        _dbContext.Medicines.Add(medicine);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await FindByIdAsync(medicine.Id, cancellationToken))!;
    }

    public async Task<MedicineDto> UpdateAsync(
        Guid id,
        MedicineInput input,
        CancellationToken cancellationToken)
    {
        var medicine = await _dbContext.Medicines
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (medicine is null)
        {
            throw new NotFoundException("medicine.not_found", $"Medicine '{id}' was not found.");
        }

        await EnsureReferencesExistAsync(input.ManufacturerId, input.CategoryId, cancellationToken);
        await EnsureBarcodeUniqueAsync(NormalizeBarcode(input.Barcode), excludeId: id, cancellationToken);

        medicine.BrandName = input.BrandName.Trim();
        medicine.GenericName = input.GenericName.Trim();
        medicine.Strength = input.Strength.Trim();
        medicine.DosageForm = input.DosageForm;
        medicine.ManufacturerId = input.ManufacturerId;
        medicine.CategoryId = input.CategoryId;
        medicine.Barcode = NormalizeBarcode(input.Barcode);
        medicine.Unit = input.Unit.Trim();
        medicine.PackSize = input.PackSize;
        medicine.PurchasePrice = input.PurchasePrice;
        medicine.SellingPrice = input.SellingPrice;
        medicine.ReorderLevel = input.ReorderLevel;
        medicine.VatRate = input.VatRate;
        medicine.RequiresPrescription = input.RequiresPrescription;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await FindByIdAsync(medicine.Id, cancellationToken))!;
    }

    public async Task<MedicineDto> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var medicine = await _dbContext.Medicines
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (medicine is null)
        {
            throw new NotFoundException("medicine.not_found", $"Medicine '{id}' was not found.");
        }

        medicine.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await FindByIdAsync(medicine.Id, cancellationToken))!;
    }

    public Task<int> CountAsync(CancellationToken cancellationToken)
        => _dbContext.Medicines.CountAsync(cancellationToken);

    public Task<int> CountActiveAsync(CancellationToken cancellationToken)
        => _dbContext.Medicines.CountAsync(medicine => medicine.IsActive, cancellationToken);

    private async Task EnsureReferencesExistAsync(
        Guid? manufacturerId,
        Guid? categoryId,
        CancellationToken cancellationToken)
    {
        if (manufacturerId.HasValue
            && !await _dbContext.Manufacturers.AnyAsync(
                candidate => candidate.Id == manufacturerId.Value,
                cancellationToken))
        {
            throw new NotFoundException(
                "manufacturer.not_found",
                $"Manufacturer '{manufacturerId}' was not found in this tenant.");
        }

        if (categoryId.HasValue
            && !await _dbContext.MedicineCategories.AnyAsync(
                candidate => candidate.Id == categoryId.Value,
                cancellationToken))
        {
            throw new NotFoundException(
                "category.not_found",
                $"Category '{categoryId}' was not found in this tenant.");
        }
    }

    private async Task EnsureBarcodeUniqueAsync(
        string? barcode,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        if (barcode is null)
        {
            return;
        }

        var duplicate = await _dbContext.Medicines.AnyAsync(
            candidate => candidate.Barcode == barcode
                && (!excludeId.HasValue || candidate.Id != excludeId.Value),
            cancellationToken);

        if (duplicate)
        {
            throw new ConflictException(
                "medicine.barcode.duplicate",
                $"Another medicine with barcode '{barcode}' already exists in this tenant.");
        }
    }

    private static MedicineDto ToDto(Medicine medicine)
        => new(
            Id: medicine.Id,
            BrandName: medicine.BrandName,
            GenericName: medicine.GenericName,
            Strength: medicine.Strength,
            DosageForm: medicine.DosageForm,
            ManufacturerId: medicine.ManufacturerId,
            ManufacturerName: medicine.Manufacturer?.Name,
            CategoryId: medicine.CategoryId,
            CategoryName: medicine.Category?.Name,
            Barcode: medicine.Barcode,
            Unit: medicine.Unit,
            PackSize: medicine.PackSize,
            PurchasePrice: medicine.PurchasePrice,
            SellingPrice: medicine.SellingPrice,
            ReorderLevel: medicine.ReorderLevel,
            VatRate: medicine.VatRate,
            RequiresPrescription: medicine.RequiresPrescription,
            IsActive: medicine.IsActive);

    private static string? NormalizeBarcode(string? barcode)
        => string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();

    private static string EscapeLike(string value)
        => value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
