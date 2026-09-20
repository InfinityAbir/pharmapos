using Microsoft.EntityFrameworkCore;
using PharmaPOS.Application.Common.Sales;
using PharmaPOS.Application.Features.Catalog;
using PharmaPOS.Application.Features.Sales;
using PharmaPOS.Domain.Sales;
using PharmaPOS.Infrastructure.Persistence;

namespace PharmaPOS.Infrastructure.Sales;

/// <summary>Tenant-scoped sale history and receipts (reads only; sales are immutable).</summary>
public sealed class SaleReader : ISaleReader
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public SaleReader(AppDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<SaleDto>> ListAsync(
        DateOnly? from,
        DateOnly? to,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        // Date filters are tenant-local days (same convention as the today summary).
        var timezone = await ResolveTimezoneAsync(cancellationToken);
        var query = _dbContext.Sales.AsNoTracking().AsQueryable();

        if (from.HasValue)
        {
            var start = DayStartUtc(from.Value, timezone);
            query = query.Where(sale => sale.OccurredAtUtc >= start);
        }

        if (to.HasValue)
        {
            var endExclusive = DayStartUtc(to.Value.AddDays(1), timezone);
            query = query.Where(sale => sale.OccurredAtUtc < endExclusive);
        }

        var ids = await query
            .OrderByDescending(sale => sale.OccurredAtUtc)
            .Skip(skip)
            .Take(take)
            .Select(sale => sale.Id)
            .ToListAsync(cancellationToken);

        var dtos = new List<SaleDto>(ids.Count);
        foreach (var id in ids)
        {
            dtos.Add((await FindDtoAsync(_dbContext, id, cancellationToken))!);
        }

        return dtos;
    }

    public Task<SaleDto?> FindAsync(Guid id, CancellationToken cancellationToken)
        => FindDtoAsync(_dbContext, id, cancellationToken);

    public async Task<ReceiptDto?> GetReceiptAsync(Guid id, CancellationToken cancellationToken)
    {
        var sale = await FindDtoAsync(_dbContext, id, cancellationToken);
        if (sale is null)
        {
            return null;
        }

        var profile = await _dbContext.PharmacyProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var pharmacy = profile is null
            ? new PharmacyProfileDto(
                PharmacyName: "Pharmacy",
                Address: null, Phone: null, Email: null, Website: null,
                RegistrationInfo: null, VatInfo: null, InvoiceFooter: null,
                Currency: "BDT", DefaultLanguage: "en", Timezone: "Asia/Dhaka",
                LogoUrl: null)
            : new PharmacyProfileDto(
                profile.PharmacyName, profile.Address, profile.Phone, profile.Email,
                profile.Website, profile.RegistrationInfo, profile.VatInfo,
                profile.InvoiceFooter, profile.Currency, profile.DefaultLanguage,
                profile.Timezone, profile.LogoUrl);

        return new ReceiptDto(pharmacy, sale);
    }

    public async Task<TodaySalesDto> GetTodaySummaryAsync(CancellationToken cancellationToken)
    {
        // "Today" is the tenant's day, not UTC: a sale at 00:30 Dhaka time belongs to the
        // local day. An unusable timezone falls back to UTC rather than failing the dashboard.
        var (today, start, endExclusive) =
            await TodayBoundsAsync(_timeProvider.GetUtcNow(), cancellationToken);

        var sales = await _dbContext.Sales
            .AsNoTracking()
            .Where(sale => sale.OccurredAtUtc >= start && sale.OccurredAtUtc < endExclusive)
            .ToListAsync(cancellationToken);

        return new TodaySalesDto(
            Date: today,
            SaleCount: sales.Count,
            TotalAmount: sales.Sum(sale => sale.TotalAmount));
    }

    private async Task<(DateOnly Date, DateTimeOffset Start, DateTimeOffset EndExclusive)>
        TodayBoundsAsync(DateTimeOffset utcNow, CancellationToken cancellationToken)
    {
        var timezone = await ResolveTimezoneAsync(cancellationToken);

        try
        {
            var localNow = TimeZoneInfo.ConvertTime(utcNow, timezone);
            var start = DayStartUtc(DateOnly.FromDateTime(localNow.Date), timezone);
            var endExclusive = start.AddDays(1);

            return (DateOnly.FromDateTime(localNow.Date), start, endExclusive);
        }
        catch (ArgumentException)
        {
            // DST gap/ambiguity on exotic zones: fall back to the UTC day.
            var today = DateOnly.FromDateTime(utcNow.Date);
            var start = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            return (today, start, start.AddDays(1));
        }
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

    internal static async Task<SaleDto?> FindDtoAsync(
        AppDbContext db,
        Guid id,
        CancellationToken cancellationToken)
    {
        var sale = await db.Sales
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (sale is null)
        {
            return null;
        }

        var items = await db.SaleItems
            .AsNoTracking()
            .Include(item => item.Medicine)
            .Include(item => item.Batch)
            .Where(item => item.SaleId == id)
            .OrderBy(item => item.Medicine.BrandName)
            .ThenBy(item => item.Batch.BatchNumber)
            .ToListAsync(cancellationToken);

        var payments = await db.SalePayments
            .AsNoTracking()
            .Where(payment => payment.SaleId == id)
            .OrderBy(payment => payment.Method)
            .ToListAsync(cancellationToken);

        return new SaleDto(
            Id: sale.Id,
            InvoiceNumber: sale.InvoiceNumber,
            OccurredAtUtc: sale.OccurredAtUtc,
            Subtotal: sale.Subtotal,
            DiscountAmount: sale.DiscountAmount,
            TotalAmount: sale.TotalAmount,
            PaidAmount: sale.PaidAmount,
            ChangeDue: sale.PaidAmount - sale.TotalAmount,
            CustomerName: sale.CustomerName,
            CustomerPhone: sale.CustomerPhone,
            PrescriptionReference: sale.PrescriptionReference,
            CashierSubject: sale.CashierSubject,
            Items: items.Select(item => new SaleItemDto(
                Id: item.Id,
                MedicineId: item.MedicineId,
                MedicineName: item.Medicine is null
                    ? item.MedicineId.ToString()
                    : $"{item.Medicine.BrandName} {item.Medicine.Strength}",
                BatchId: item.BatchId,
                BatchNumber: item.Batch?.BatchNumber ?? item.BatchId.ToString(),
                Quantity: item.Quantity,
                UnitPrice: item.UnitPrice,
                DiscountAmount: item.DiscountAmount,
                LineTotal: item.Quantity * item.UnitPrice - item.DiscountAmount)).ToArray(),
            Payments: payments.Select(payment => new SalePaymentDto(
                Id: payment.Id,
                Method: payment.Method,
                Amount: payment.Amount,
                Reference: payment.Reference)).ToArray());
    }
}
