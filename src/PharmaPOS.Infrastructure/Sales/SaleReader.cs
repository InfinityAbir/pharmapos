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

    public SaleReader(AppDbContext dbContext)
        => _dbContext = dbContext;

    public async Task<IReadOnlyList<SaleDto>> ListAsync(
        DateOnly? from,
        DateOnly? to,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Sales.AsNoTracking().AsQueryable();

        if (from.HasValue)
        {
            var start = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(sale => sale.OccurredAtUtc >= start);
        }

        if (to.HasValue)
        {
            var endExclusive = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
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
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var start = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endExclusive = today.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var sales = await _dbContext.Sales
            .AsNoTracking()
            .Where(sale => sale.OccurredAtUtc >= start && sale.OccurredAtUtc < endExclusive)
            .ToListAsync(cancellationToken);

        return new TodaySalesDto(
            Date: today,
            SaleCount: sales.Count,
            TotalAmount: sales.Sum(sale => sale.TotalAmount));
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
