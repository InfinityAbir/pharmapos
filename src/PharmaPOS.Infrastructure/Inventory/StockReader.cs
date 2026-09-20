using Microsoft.EntityFrameworkCore;
using PharmaPOS.Application.Common.Inventory;
using PharmaPOS.Application.Features.Inventory;
using PharmaPOS.Domain.Catalog;
using PharmaPOS.Domain.Inventory;
using PharmaPOS.Infrastructure.Persistence;

namespace PharmaPOS.Infrastructure.Inventory;

/// <summary>
/// Derived stock reads. Medicine totals are summed from batches on every read — stock is never
/// stored separately, so reads cannot disagree with the ledger.
/// </summary>
public sealed class StockReader : IStockReader
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public StockReader(AppDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<MedicineStockDto?> GetMedicineStockAsync(
        Guid medicineId,
        CancellationToken cancellationToken)
    {
        var medicine = await _dbContext.Medicines
            .AsNoTracking()
            .Include(candidate => candidate.Manufacturer)
            .Include(candidate => candidate.Category)
            .FirstOrDefaultAsync(candidate => candidate.Id == medicineId, cancellationToken);

        if (medicine is null)
        {
            return null;
        }

        var batches = await BatchesForAsync([medicineId], cancellationToken);

        return ToStock(medicine, batches);
    }

    public async Task<IReadOnlyList<MedicineStockDto>> ListStockAsync(
        string? search,
        bool lowStockOnly,
        CancellationToken cancellationToken)
    {
        var medicines = await LoadMedicinesAsync(search, cancellationToken);
        var stocks = await ToStocksAsync(medicines, cancellationToken);

        return stocks
            .Where(stock => !lowStockOnly || stock.IsLowStock)
            .ToArray();
    }

    public async Task<IReadOnlyList<MedicineStockDto>> ListLowStockAsync(
        CancellationToken cancellationToken)
    {
        var medicines = await LoadMedicinesAsync(null, cancellationToken);
        var stocks = await ToStocksAsync(medicines, cancellationToken);

        return stocks.Where(stock => stock.IsLowStock).ToArray();
    }

    public async Task<IReadOnlyList<BatchDto>> ListExpiringAsync(
        int withinDays,
        CancellationToken cancellationToken)
    {
        var today = Today();
        var horizon = today.AddDays(withinDays);

        var batches = await _dbContext.MedicineBatches
            .AsNoTracking()
            .Include(batch => batch.Medicine)
            .Where(batch => batch.Quantity > 0
                && batch.ExpiryDate > today
                && batch.ExpiryDate <= horizon)
            .OrderBy(batch => batch.ExpiryDate)
            .ThenBy(batch => batch.BatchNumber)
            .ToListAsync(cancellationToken);

        return batches.Select(batch => ToBatch(batch, today)).ToArray();
    }

    public async Task<IReadOnlyList<BatchDto>> ListExpiredAsync(
        CancellationToken cancellationToken)
    {
        var today = Today();

        var batches = await _dbContext.MedicineBatches
            .AsNoTracking()
            .Include(batch => batch.Medicine)
            .Where(batch => batch.Quantity > 0 && batch.ExpiryDate <= today)
            .OrderBy(batch => batch.ExpiryDate)
            .ThenBy(batch => batch.BatchNumber)
            .ToListAsync(cancellationToken);

        return batches.Select(batch => ToBatch(batch, today)).ToArray();
    }

    public async Task<AlertsSummaryDto> GetAlertsSummaryAsync(
        CancellationToken cancellationToken)
    {
        var today = Today();
        var horizon = today.AddDays(30);

        var lowStock = (await ListLowStockAsync(cancellationToken)).Count;

        var expiring = await _dbContext.MedicineBatches.CountAsync(
            batch => batch.Quantity > 0
                && batch.ExpiryDate > today
                && batch.ExpiryDate <= horizon,
            cancellationToken);

        var expired = await _dbContext.MedicineBatches.CountAsync(
            batch => batch.Quantity > 0 && batch.ExpiryDate <= today,
            cancellationToken);

        return new AlertsSummaryDto(
            LowStockCount: lowStock,
            ExpiringWithin30DaysCount: expiring,
            ExpiredBatchCount: expired);
    }

    private async Task<List<Medicine>> LoadMedicinesAsync(
        string? search,
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
                || EF.Functions.ILike(medicine.GenericName, pattern));
        }

        return await query
            .OrderBy(medicine => medicine.BrandName)
            .ThenBy(medicine => medicine.Strength)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<MedicineStockDto>> ToStocksAsync(
        List<Medicine> medicines,
        CancellationToken cancellationToken)
    {
        var ids = medicines.Select(medicine => medicine.Id).ToArray();
        var batches = await BatchesForAsync(ids, cancellationToken);
        var byMedicine = batches
            .GroupBy(batch => batch.MedicineId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return medicines
            .Select(medicine => ToStock(
                medicine,
                byMedicine.TryGetValue(medicine.Id, out var list) ? list : []))
            .ToList();
    }

    private Task<List<MedicineBatch>> BatchesForAsync(
        Guid[] medicineIds,
        CancellationToken cancellationToken)
        => _dbContext.MedicineBatches
            .AsNoTracking()
            .Where(batch => medicineIds.Contains(batch.MedicineId))
            .OrderBy(batch => batch.ExpiryDate)
            .ThenBy(batch => batch.BatchNumber)
            .ToListAsync(cancellationToken);

    private MedicineStockDto ToStock(Medicine medicine, List<MedicineBatch> batches)
    {
        var today = Today();
        var total = batches.Sum(batch => batch.Quantity);

        return new MedicineStockDto(
            MedicineId: medicine.Id,
            BrandName: medicine.BrandName,
            Strength: medicine.Strength,
            TotalQuantity: total,
            ReorderLevel: medicine.ReorderLevel,
            IsLowStock: total < medicine.ReorderLevel,
            BatchCount: batches.Count,
            Batches: batches.Select(batch => ToBatch(batch, today)).ToArray());
    }

    private static BatchDto ToBatch(MedicineBatch batch, DateOnly today)
        => new(
            Id: batch.Id,
            MedicineId: batch.MedicineId,
            MedicineName: batch.Medicine is null
                ? batch.MedicineId.ToString()
                : $"{batch.Medicine.BrandName} {batch.Medicine.Strength}",
            BatchNumber: batch.BatchNumber,
            PurchasePrice: batch.PurchasePrice,
            SellingPrice: batch.SellingPrice,
            Quantity: batch.Quantity,
            ManufactureDate: batch.ManufactureDate,
            ExpiryDate: batch.ExpiryDate,
            SupplierName: batch.SupplierName,
            PurchaseReference: batch.PurchaseReference,
            IsExpired: batch.ExpiryDate <= today);

    private DateOnly Today() => DateOnly.FromDateTime(_timeProvider.GetUtcNow().Date);

    private static string EscapeLike(string value)
        => value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
