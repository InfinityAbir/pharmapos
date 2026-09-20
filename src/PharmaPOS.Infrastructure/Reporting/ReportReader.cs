using Microsoft.EntityFrameworkCore;
using Npgsql;
using PharmaPOS.Application.Common.Reporting;
using PharmaPOS.Application.Features.Inventory;
using PharmaPOS.Application.Features.Reports;
using PharmaPOS.Domain.Purchasing;
using PharmaPOS.Infrastructure.Persistence;

namespace PharmaPOS.Infrastructure.Reporting;

/// <summary>
/// Tenant-scoped dashboard/report reads. Every aggregate runs in PostgreSQL
/// (SUM/COUNT/GROUP BY over indexed, tenant-filtered sets with narrow projections);
/// only compact DTOs leave the database. No method materializes the underlying
/// sales, batches, movements, or purchases.
/// </summary>
public sealed class ReportReader : IReportReader
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public ReportReader(AppDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<SalesTrendBucketDto>> GetSalesTrendAsync(
        DateOnly from,
        DateOnly to,
        string bucket,
        CancellationToken cancellationToken)
    {
        var timezone = await ResolveTimezoneAsync(cancellationToken);
        var startUtc = DayStartUtc(from, timezone);
        var endUtc = DayStartUtc(to.AddDays(1), timezone);
        var tenantId = TenantScope.CurrentTenantId(_dbContext);

        // date_trunc takes no bound unit parameter: one constant statement per
        // allow-listed bucket. Global query filters do not apply to raw SQL,
        // so the tenant predicate is explicit.
        var sql = bucket switch
        {
            "week" => "SELECT (date_trunc('week', \"OccurredAtUtc\" AT TIME ZONE {1}))::date AS \"Period\", " +
                "COUNT(*)::int AS \"SaleCount\", " +
                "COALESCE(SUM(\"TotalAmount\"), 0) AS \"TotalAmount\" " +
                "FROM sales WHERE \"TenantId\" = {0} " +
                "AND \"OccurredAtUtc\" >= {2} AND \"OccurredAtUtc\" < {3} " +
                "GROUP BY 1 ORDER BY 1",
            "month" => "SELECT (date_trunc('month', \"OccurredAtUtc\" AT TIME ZONE {1}))::date AS \"Period\", " +
                "COUNT(*)::int AS \"SaleCount\", " +
                "COALESCE(SUM(\"TotalAmount\"), 0) AS \"TotalAmount\" " +
                "FROM sales WHERE \"TenantId\" = {0} " +
                "AND \"OccurredAtUtc\" >= {2} AND \"OccurredAtUtc\" < {3} " +
                "GROUP BY 1 ORDER BY 1",
            _ => "SELECT (date_trunc('day', \"OccurredAtUtc\" AT TIME ZONE {1}))::date AS \"Period\", " +
                "COUNT(*)::int AS \"SaleCount\", " +
                "COALESCE(SUM(\"TotalAmount\"), 0) AS \"TotalAmount\" " +
                "FROM sales WHERE \"TenantId\" = {0} " +
                "AND \"OccurredAtUtc\" >= {2} AND \"OccurredAtUtc\" < {3} " +
                "GROUP BY 1 ORDER BY 1",
        };

        async Task<List<TrendRow>> QueryAsync(string timezoneId)
            => await _dbContext.Database.SqlQueryRaw<TrendRow>(
                sql, tenantId, timezoneId, startUtc, endUtc).ToListAsync(cancellationToken);

        List<TrendRow> rows;
        try
        {
            rows = await QueryAsync(timezone.Id);
        }
        catch (PostgresException)
        {
            // The API host resolved a timezone name PostgreSQL does not know
            // (e.g. a Windows id while developing on Windows): fall back to UTC
            // bucketing rather than failing the report.
            rows = await QueryAsync("UTC");
        }

        return rows.Select(row => new SalesTrendBucketDto(
            row.Period, row.SaleCount, row.TotalAmount)).ToArray();
    }

    public async Task<IReadOnlyList<SalesByCashierRowDto>> GetSalesByCashierAsync(
        DateOnly from,
        DateOnly to,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var timezone = await ResolveTimezoneAsync(cancellationToken);
        var startUtc = DayStartUtc(from, timezone);
        var endUtc = DayStartUtc(to.AddDays(1), timezone);

        return await _dbContext.Sales
            .AsNoTracking()
            .Where(sale => sale.OccurredAtUtc >= startUtc && sale.OccurredAtUtc < endUtc)
            .GroupBy(sale => sale.CashierSubject ?? "Unknown")
            .Select(group => new SalesByCashierRowDto(
                group.Key,
                group.Count(),
                group.Sum(sale => sale.TotalAmount)))
            .OrderByDescending(row => row.TotalAmount)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SalesByMethodRowDto>> GetSalesByMethodAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var timezone = await ResolveTimezoneAsync(cancellationToken);
        var startUtc = DayStartUtc(from, timezone);
        var endUtc = DayStartUtc(to.AddDays(1), timezone);

        return await _dbContext.SalePayments
            .AsNoTracking()
            .Where(payment => payment.Sale.OccurredAtUtc >= startUtc
                && payment.Sale.OccurredAtUtc < endUtc)
            .GroupBy(payment => payment.Method)
            .Select(group => new SalesByMethodRowDto(
                group.Key,
                group.Count(),
                group.Sum(payment => payment.Amount)))
            .OrderByDescending(row => row.TotalAmount)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SalesByMedicineRowDto>> GetSalesByMedicineAsync(
        DateOnly from,
        DateOnly to,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var timezone = await ResolveTimezoneAsync(cancellationToken);
        var startUtc = DayStartUtc(from, timezone);
        var endUtc = DayStartUtc(to.AddDays(1), timezone);

        return await _dbContext.SaleItems
            .AsNoTracking()
            .Where(item => item.Sale.OccurredAtUtc >= startUtc
                && item.Sale.OccurredAtUtc < endUtc)
            .GroupBy(item => new
            {
                item.MedicineId,
                item.Medicine.BrandName,
                item.Medicine.Strength,
            })
            .Select(group => new SalesByMedicineRowDto(
                group.Key.MedicineId,
                group.Key.BrandName + " " + group.Key.Strength,
                group.Sum(item => item.Quantity),
                group.Sum(item => item.Quantity * item.UnitPrice - item.DiscountAmount)))
            .OrderByDescending(row => row.Revenue)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductPerformanceRowDto>> GetProductPerformanceAsync(
        int days,
        bool bestFirst,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var since = _timeProvider.GetUtcNow().AddDays(-days);

        // Outer aggregate: active medicines with zero window sales still surface
        // (they are the slow movers). Both sums evaluate in a single SQL statement.
        var query = _dbContext.Medicines
            .AsNoTracking()
            .Where(medicine => medicine.IsActive)
            .Select(medicine => new ProductPerformanceRowDto(
                medicine.Id,
                medicine.BrandName + " " + medicine.Strength,
                _dbContext.SaleItems
                    .Where(item => item.MedicineId == medicine.Id
                        && item.Sale.OccurredAtUtc >= since)
                    .Sum(item => (int?)item.Quantity) ?? 0,
                _dbContext.SaleItems
                    .Where(item => item.MedicineId == medicine.Id
                        && item.Sale.OccurredAtUtc >= since)
                    .Sum(item => (decimal?)(item.Quantity * item.UnitPrice - item.DiscountAmount)) ?? 0m));

        query = bestFirst
            ? query.OrderByDescending(row => row.QuantitySold).ThenBy(row => row.MedicineName)
            : query.OrderBy(row => row.QuantitySold).ThenBy(row => row.MedicineName);

        return await query.Skip(skip).Take(take).ToListAsync(cancellationToken);
    }

    public async Task<StockValuationDto> GetStockValuationAsync(
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var batches = _dbContext.MedicineBatches.AsNoTracking();

        var totals = await batches
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Medicines = group.Select(batch => batch.MedicineId).Distinct().Count(),
                Units = group.Sum(batch => (long?)batch.Quantity) ?? 0L,
                Purchase = group.Sum(batch => (decimal?)(batch.Quantity * batch.PurchasePrice)) ?? 0m,
                Selling = group.Sum(batch => (decimal?)(batch.Quantity * batch.SellingPrice)) ?? 0m,
            })
            .SingleOrDefaultAsync(cancellationToken);

        var rows = await batches
            .GroupBy(batch => new
            {
                batch.MedicineId,
                batch.Medicine.BrandName,
                batch.Medicine.Strength,
            })
            .Select(group => new StockValuationRowDto(
                group.Key.MedicineId,
                group.Key.BrandName + " " + group.Key.Strength,
                group.Sum(batch => batch.Quantity),
                group.Sum(batch => batch.Quantity * batch.PurchasePrice),
                group.Sum(batch => batch.Quantity * batch.SellingPrice)))
            .OrderByDescending(row => row.SellingValue)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return new StockValuationDto(
            totals?.Medicines ?? 0,
            totals?.Units ?? 0L,
            totals?.Purchase ?? 0m,
            totals?.Selling ?? 0m,
            rows);
    }

    public async Task<IReadOnlyList<LowStockRowDto>> GetLowStockReportAsync(
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        // Grouped batch sums compared against the reorder level in SQL — the report path
        // never loads the full catalog the way the operational low-stock list does.
        return await _dbContext.Medicines
            .AsNoTracking()
            .Where(medicine => medicine.IsActive)
            .Select(medicine => new
            {
                medicine.Id,
                Name = medicine.BrandName + " " + medicine.Strength,
                medicine.ReorderLevel,
                Total = _dbContext.MedicineBatches
                    .Where(batch => batch.MedicineId == medicine.Id)
                    .Sum(batch => (int?)batch.Quantity) ?? 0,
            })
            .Where(row => row.Total < row.ReorderLevel)
            .OrderBy(row => row.Total)
            .Select(row => new LowStockRowDto(
                row.Id, row.Name, row.Total, row.ReorderLevel, row.ReorderLevel - row.Total))
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MovementDto>> GetMovementReportAsync(
        Guid? medicineId,
        Guid? batchId,
        string? type,
        DateOnly? from,
        DateOnly? to,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.InventoryMovements
            .AsNoTracking()
            .Include(movement => movement.Medicine)
            .Include(movement => movement.Batch)
            .AsQueryable();

        if (medicineId.HasValue)
        {
            query = query.Where(movement => movement.MedicineId == medicineId.Value);
        }

        if (batchId.HasValue)
        {
            query = query.Where(movement => movement.BatchId == batchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(movement => movement.Type == type);
        }

        if (from.HasValue && to.HasValue)
        {
            var timezone = await ResolveTimezoneAsync(cancellationToken);
            var startUtc = DayStartUtc(from.Value, timezone);
            var endUtc = DayStartUtc(to.Value.AddDays(1), timezone);
            query = query.Where(movement =>
                movement.OccurredAtUtc >= startUtc && movement.OccurredAtUtc < endUtc);
        }

        // Narrow projection: only the columns the ledger table renders.
        return await query
            .OrderByDescending(movement => movement.OccurredAtUtc)
            .Skip(skip)
            .Take(take)
            .Select(movement => new MovementDto(
                movement.Id,
                movement.MedicineId,
                movement.Medicine.BrandName + " " + movement.Medicine.Strength,
                movement.BatchId,
                movement.Batch.BatchNumber,
                movement.Type,
                movement.QuantityChange,
                movement.QuantityAfter,
                movement.Reason,
                movement.ActorSubject,
                movement.OccurredAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PurchasesBySupplierRowDto>> GetPurchasesBySupplierAsync(
        DateOnly from,
        DateOnly to,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        // PurchaseDate is a tenant-local date column: direct comparison, no tz mapping.
        return await _dbContext.Purchases
            .AsNoTracking()
            .Where(purchase => purchase.Status == PurchaseStatus.Received
                && purchase.PurchaseDate >= from
                && purchase.PurchaseDate <= to)
            .GroupBy(purchase => new { purchase.SupplierId, purchase.Supplier.Name })
            .Select(group => new PurchasesBySupplierRowDto(
                group.Key.SupplierId,
                group.Key.Name,
                group.Count(),
                group.Sum(purchase => purchase.TotalAmount)))
            .OrderByDescending(row => row.TotalAmount)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<PurchaseTotalsDto> GetPurchaseTotalsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var received = _dbContext.Purchases
            .AsNoTracking()
            .Where(purchase => purchase.Status == PurchaseStatus.Received
                && purchase.PurchaseDate >= from
                && purchase.PurchaseDate <= to);

        var receivedCount = await received.CountAsync(cancellationToken);
        var receivedTotal = await received.SumAsync(
            purchase => (decimal?)purchase.TotalAmount, cancellationToken) ?? 0m;
        var draftCount = await _dbContext.Purchases
            .AsNoTracking()
            .CountAsync(purchase => purchase.Status == PurchaseStatus.Draft, cancellationToken);

        return new PurchaseTotalsDto(from, to, receivedCount, receivedTotal, draftCount);
    }

    private async Task<TimeZoneInfo> ResolveTimezoneAsync(CancellationToken cancellationToken)
    {
        var timezoneId = await _dbContext.PharmacyProfiles
            .AsNoTracking()
            .Select(profile => profile.Timezone)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(timezoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static DateTimeOffset DayStartUtc(DateOnly date, TimeZoneInfo timezone)
    {
        var localMidnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var offset = timezone.GetUtcOffset(localMidnight);
        return new DateTimeOffset(localMidnight, offset).ToUniversalTime();
    }

    /// <summary>Shape of the trend GROUP BY row (raw SQL bypasses entity mapping).</summary>
    private sealed class TrendRow
    {
        public DateOnly Period { get; set; }

        public int SaleCount { get; set; }

        public decimal TotalAmount { get; set; }
    }
}
