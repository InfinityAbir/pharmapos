using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PharmaPOS.Application.Common.Sales;
using PharmaPOS.Application.Features.Returns;
using PharmaPOS.Domain.Exceptions;
using PharmaPOS.Domain.Inventory;
using PharmaPOS.Domain.Sales;
using PharmaPOS.Infrastructure.Persistence;

namespace PharmaPOS.Infrastructure.Sales;

/// <summary>
/// Sale returns: exactly one atomic transaction per return (return + items + batch restock +
/// SALE_RETURN movements + idempotency record). The original sale is never edited. Refund
/// money is computed server-side pro-rata from the original line nets including the header
/// discount share; cumulative refunds can never exceed the sale total.
/// </summary>
public sealed class ReturnService : IReturnService
{
    private readonly AppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IReturnReader _returnReader;

    public ReturnService(
        AppDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        IReturnReader returnReader)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _returnReader = returnReader;
    }

    public async Task<ProcessReturnResult> ProcessAsync(
        ReturnInput input,
        CancellationToken cancellationToken)
    {
        var key = input.IdempotencyKey.Trim();

        var replay = await _dbContext.SaleReturnIdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Key == key, cancellationToken);

        if (replay is not null)
        {
            var stored = await _returnReader.FindAsync(replay.SaleReturnId, cancellationToken);
            if (stored is null)
            {
                throw new ConflictException(
                    "return.replay_missing",
                    "A previous return is recorded under this key but its return is missing. " +
                    "Retry with a new idempotency key.");
            }

            return new ProcessReturnResult(stored, Replayed: true);
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var sale = await _dbContext.Sales
                .FirstOrDefaultAsync(candidate => candidate.Id == input.SaleId, cancellationToken);

            if (sale is null)
            {
                throw new NotFoundException(
                    "sale.not_found",
                    "Original sale not found in this tenant.");
            }

            var tenantId = TenantScope.CurrentTenantId(_dbContext);
            var actor = Actor();
            var now = DateTimeOffset.UtcNow;
            var reason = input.Reason.Trim();
            var refundReference = Normalize(input.RefundReference);

            if (PaymentMethods.RequiresReference(input.RefundMethod)
                && string.IsNullOrWhiteSpace(refundReference))
            {
                throw new BusinessRuleException(
                    "return.reference_required",
                    "A transaction reference is required for non-cash refunds.");
            }

            var saleItems = await _dbContext.SaleItems
                .Where(item => item.SaleId == sale.Id)
                .ToListAsync(cancellationToken);

            if (saleItems.Count == 0)
            {
                throw new BusinessRuleException(
                    "return.no_items",
                    "Original sale has no items to return.");
            }

            var byId = saleItems.ToDictionary(item => item.Id);

            var alreadyReturned = await _dbContext.SaleReturnItems
                .AsNoTracking()
                .Where(item => item.SaleReturn.SaleId == sale.Id)
                .GroupBy(item => item.SaleItemId)
                .Select(group => new { SaleItemId = group.Key, Quantity = group.Sum(item => item.Quantity) })
                .ToDictionaryAsync(row => row.SaleItemId, row => row.Quantity, cancellationToken);

            var alreadyRefunded = await _dbContext.SaleReturns
                .AsNoTracking()
                .Where(saleReturn => saleReturn.SaleId == sale.Id)
                .SumAsync(saleReturn => (decimal?)saleReturn.RefundAmount, cancellationToken)
                ?? 0;

            // Header-discount share: total = discountedSubtotal - headerDiscount.
            var subtotal = saleItems.Sum(item => item.Quantity * item.UnitPrice);
            var lineDiscountTotal = saleItems.Sum(item => item.DiscountAmount);
            var discountedSubtotal = subtotal - lineDiscountTotal;
            var headerDiscount = sale.DiscountAmount - lineDiscountTotal;

            var saleReturn = new SaleReturn
            {
                TenantId = tenantId,
                SaleId = sale.Id,
                Reason = reason,
                Restock = input.Restock,
                RefundMethod = input.RefundMethod,
                RefundReference = refundReference,
                OccurredAtUtc = now,
                ActorSubject = actor,
            };

            var exactRefunds = new List<(SaleReturnItem Item, decimal Exact)>();
            var seen = new HashSet<Guid>();

            foreach (var line in input.Items)
            {
                if (!seen.Add(line.SaleItemId))
                {
                    throw new BusinessRuleException(
                        "return.duplicate_item",
                        "Each sold line may appear at most once per return.");
                }

                if (!byId.TryGetValue(line.SaleItemId, out var original))
                {
                    throw new BusinessRuleException(
                        "return.item_not_in_sale",
                        "One or more returned lines do not belong to the original sale.");
                }

                if (line.Quantity <= 0)
                {
                    throw new BusinessRuleException(
                        "return.quantity_invalid",
                        "Return quantity must be positive.");
                }

                alreadyReturned.TryGetValue(original.Id, out var returnedSoFar);
                var returnable = original.Quantity - returnedSoFar;

                if (line.Quantity > returnable)
                {
                    throw new BusinessRuleException(
                        "return.quantity_exceeded",
                        $"Cannot return {line.Quantity} units: only {returnable} returnable " +
                        $"on this line (sold {original.Quantity}, already returned {returnedSoFar}).");
                }

                var rowNet = original.Quantity * original.UnitPrice - original.DiscountAmount;
                var headerShare = discountedSubtotal > 0
                    ? headerDiscount * rowNet / discountedSubtotal
                    : 0;
                var rowTotalNet = rowNet - headerShare;
                var perUnit = original.Quantity > 0 ? rowTotalNet / original.Quantity : 0;
                var exact = perUnit * line.Quantity;

                var item = new SaleReturnItem
                {
                    TenantId = tenantId,
                    SaleItemId = original.Id,
                    MedicineId = original.MedicineId,
                    BatchId = original.BatchId,
                    Quantity = line.Quantity,
                    UnitPrice = original.UnitPrice,
                    RefundAmount = 0, // Assigned after rounding below.
                };

                saleReturn.Items.Add(item);
                exactRefunds.Add((item, exact));
            }

            // Round to 4dp (ToZero) with the remainder on the last row so parts sum exactly.
            var exactTotal = exactRefunds.Sum(entry => entry.Exact);
            decimal assigned = 0;
            for (var index = 0; index < exactRefunds.Count; index++)
            {
                var (item, exact) = exactRefunds[index];
                item.RefundAmount = index == exactRefunds.Count - 1
                    ? exactTotal - assigned
                    : Math.Round(exact, 4, MidpointRounding.ToZero);
                assigned += item.RefundAmount;
            }

            var refundTotal = saleReturn.Items.Sum(item => item.RefundAmount);

            if (alreadyRefunded + refundTotal > sale.TotalAmount + 0.0001m)
            {
                throw new BusinessRuleException(
                    "return.refund_exceeded",
                    $"Refund {refundTotal:F4} would exceed the sale total {sale.TotalAmount:F4} " +
                    $"(already refunded {alreadyRefunded:F4}).");
            }

            saleReturn.RefundAmount = refundTotal;
            _dbContext.SaleReturns.Add(saleReturn);

            if (input.Restock)
            {
                foreach (var item in saleReturn.Items)
                {
                    var batch = await _dbContext.MedicineBatches
                        .FirstOrDefaultAsync(
                            candidate => candidate.Id == item.BatchId
                                && candidate.MedicineId == item.MedicineId,
                            cancellationToken);

                    if (batch is null)
                    {
                        throw new BusinessRuleException(
                            "return.batch_missing",
                            "The original batch is missing; process the return without restock instead.");
                    }

                    batch.Quantity += item.Quantity;

                    _dbContext.InventoryMovements.Add(new InventoryMovement
                    {
                        TenantId = tenantId,
                        MedicineId = item.MedicineId,
                        BatchId = item.BatchId,
                        Type = MovementTypes.SaleReturn,
                        QuantityChange = item.Quantity,
                        QuantityAfter = batch.Quantity,
                        Reason = sale.InvoiceNumber,
                        ActorSubject = actor,
                        OccurredAtUtc = now,
                    });
                }
            }

            _dbContext.SaleReturnIdempotencyKeys.Add(new SaleReturnIdempotencyKey
            {
                TenantId = tenantId,
                Key = key,
                SaleReturnId = saleReturn.Id,
            });

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                throw new ConflictException(
                    "return.concurrent_update",
                    "Stock changed while this return was processed. Review and retry.",
                    exception);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                throw new ConflictException(
                    "return.duplicate_request",
                    "This return is already being processed under the same key. Retry to fetch the stored return.");
            }

            await transaction.CommitAsync(cancellationToken);

            var dto = await _returnReader.FindAsync(saleReturn.Id, cancellationToken);
            return new ProcessReturnResult(dto!, Replayed: false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private string? Actor()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.FindFirst("sub")?.Value ?? user?.FindFirst("preferred_username")?.Value;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation;
}
