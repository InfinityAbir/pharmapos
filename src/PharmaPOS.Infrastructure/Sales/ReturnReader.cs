using Microsoft.EntityFrameworkCore;
using PharmaPOS.Application.Common.Sales;
using PharmaPOS.Application.Features.Returns;
using PharmaPOS.Infrastructure.Persistence;

namespace PharmaPOS.Infrastructure.Sales;

/// <summary>Tenant-scoped return history and returnable-quantity reads.</summary>
public sealed class ReturnReader : IReturnReader
{
    private readonly AppDbContext _dbContext;

    public ReturnReader(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<ReturnDto>> ListAsync(
        Guid? saleId,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.SaleReturns.AsNoTracking().AsQueryable();

        if (saleId.HasValue)
        {
            query = query.Where(saleReturn => saleReturn.SaleId == saleId.Value);
        }

        var ids = await query
            .OrderByDescending(saleReturn => saleReturn.OccurredAtUtc)
            .Skip(skip)
            .Take(take)
            .Select(saleReturn => saleReturn.Id)
            .ToListAsync(cancellationToken);

        var dtos = new List<ReturnDto>(ids.Count);
        foreach (var id in ids)
        {
            dtos.Add((await FindAsync(id, cancellationToken))!);
        }

        return dtos;
    }

    public async Task<ReturnDto?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var saleReturn = await _dbContext.SaleReturns
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (saleReturn is null)
        {
            return null;
        }

        var sale = await _dbContext.Sales
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == saleReturn.SaleId, cancellationToken);

        var items = await _dbContext.SaleReturnItems
            .AsNoTracking()
            .Include(item => item.Medicine)
            .Include(item => item.Batch)
            .Where(item => item.SaleReturnId == id)
            .OrderBy(item => item.Medicine.BrandName)
            .ThenBy(item => item.Batch.BatchNumber)
            .ToListAsync(cancellationToken);

        return new ReturnDto(
            Id: saleReturn.Id,
            SaleId: saleReturn.SaleId,
            InvoiceNumber: sale?.InvoiceNumber ?? saleReturn.SaleId.ToString(),
            Reason: saleReturn.Reason,
            Restock: saleReturn.Restock,
            RefundMethod: saleReturn.RefundMethod,
            RefundReference: saleReturn.RefundReference,
            RefundAmount: saleReturn.RefundAmount,
            OccurredAtUtc: saleReturn.OccurredAtUtc,
            ActorSubject: saleReturn.ActorSubject,
            Items: items.Select(item => new ReturnItemDto(
                Id: item.Id,
                SaleItemId: item.SaleItemId,
                MedicineId: item.MedicineId,
                MedicineName: item.Medicine is null
                    ? item.MedicineId.ToString()
                    : $"{item.Medicine.BrandName} {item.Medicine.Strength}",
                BatchId: item.BatchId,
                BatchNumber: item.Batch?.BatchNumber ?? item.BatchId.ToString(),
                Quantity: item.Quantity,
                UnitPrice: item.UnitPrice,
                RefundAmount: item.RefundAmount)).ToArray());
    }

    public async Task<ReturnableSaleDto?> GetReturnableAsync(
        Guid saleId,
        CancellationToken cancellationToken)
    {
        var sale = await _dbContext.Sales
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == saleId, cancellationToken);

        if (sale is null)
        {
            return null;
        }

        var saleItems = await _dbContext.SaleItems
            .AsNoTracking()
            .Include(item => item.Medicine)
            .Include(item => item.Batch)
            .Where(item => item.SaleId == saleId)
            .OrderBy(item => item.Medicine.BrandName)
            .ThenBy(item => item.Batch.BatchNumber)
            .ToListAsync(cancellationToken);

        var returnedByItem = await _dbContext.SaleReturnItems
            .AsNoTracking()
            .Where(item => item.SaleReturn.SaleId == saleId)
            .GroupBy(item => item.SaleItemId)
            .Select(group => new { SaleItemId = group.Key, Quantity = group.Sum(item => item.Quantity) })
            .ToDictionaryAsync(row => row.SaleItemId, row => row.Quantity, cancellationToken);

        var alreadyRefunded = await _dbContext.SaleReturns
            .AsNoTracking()
            .Where(saleReturn => saleReturn.SaleId == saleId)
            .SumAsync(saleReturn => (decimal?)saleReturn.RefundAmount, cancellationToken)
            ?? 0;

        var subtotal = saleItems.Sum(item => item.Quantity * item.UnitPrice);
        var lineDiscountTotal = saleItems.Sum(item => item.DiscountAmount);
        var discountedSubtotal = subtotal - lineDiscountTotal;
        var headerDiscount = sale.DiscountAmount - lineDiscountTotal;

        var items = saleItems.Select(item =>
        {
            returnedByItem.TryGetValue(item.Id, out var returned);
            var rowNet = item.Quantity * item.UnitPrice - item.DiscountAmount;
            var headerShare = discountedSubtotal > 0
                ? headerDiscount * rowNet / discountedSubtotal
                : 0;
            var rowTotalNet = rowNet - headerShare;
            var perUnit = item.Quantity > 0 ? rowTotalNet / item.Quantity : 0;

            return new ReturnableItemDto(
                SaleItemId: item.Id,
                MedicineId: item.MedicineId,
                MedicineName: item.Medicine is null
                    ? item.MedicineId.ToString()
                    : $"{item.Medicine.BrandName} {item.Medicine.Strength}",
                BatchId: item.BatchId,
                BatchNumber: item.Batch?.BatchNumber ?? item.BatchId.ToString(),
                SoldQuantity: item.Quantity,
                AlreadyReturnedQuantity: returned,
                ReturnableQuantity: item.Quantity - returned,
                UnitPrice: item.UnitPrice,
                NetPerUnit: perUnit);
        }).ToArray();

        return new ReturnableSaleDto(
            SaleId: sale.Id,
            InvoiceNumber: sale.InvoiceNumber,
            TotalAmount: sale.TotalAmount,
            AlreadyRefundedAmount: alreadyRefunded,
            Items: items);
    }
}
