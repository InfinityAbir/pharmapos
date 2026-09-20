using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PharmaPOS.Application.Common.Sales;
using PharmaPOS.Domain.Exceptions;
using PharmaPOS.Domain.Inventory;
using PharmaPOS.Domain.Sales;
using PharmaPOS.Infrastructure.Persistence;

namespace PharmaPOS.Infrastructure.Sales;

/// <summary>
/// Checkout: exactly one atomic transaction per sale (invoice number, sale + items + payments,
/// batch deductions + <c>SALE</c> movements, idempotency record). Unit prices resolve
/// server-side from batches; quantities come from the client. FEFO allocation skips expired
/// and empty batches; optimistic-concurrency conflicts surface as 409.
/// </summary>
public sealed class CheckoutService : ICheckoutService
{
    private readonly AppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CheckoutService(AppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<CheckoutResult> CheckoutAsync(
        CheckoutInput input,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantScope.CurrentTenantId(_dbContext);
        var key = input.IdempotencyKey.Trim();

        // Replay path: return the stored sale without touching money or stock.
        var replay = await _dbContext.SaleIdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Key == key, cancellationToken);

        if (replay is not null)
        {
            var stored = await SaleReader.FindDtoAsync(_dbContext, replay.SaleId, cancellationToken);
            return new CheckoutResult(stored!, Replayed: true);
        }

        await EnsureCounterAsync(tenantId, cancellationToken);

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var invoiceNumber = await NextInvoiceNumberAsync(tenantId, cancellationToken);

            var medicineIds = input.Items.Select(item => item.MedicineId).Distinct().ToArray();
            var medicines = await _dbContext.Medicines
                .Where(medicine => medicineIds.Contains(medicine.Id))
                .ToDictionaryAsync(medicine => medicine.Id, cancellationToken);

            if (medicines.Count != medicineIds.Length)
            {
                throw new NotFoundException(
                    "medicine.not_found",
                    "One or more medicines do not exist in this tenant.");
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            var actor = Actor();
            var sale = new Sale
            {
                TenantId = tenantId,
                InvoiceNumber = invoiceNumber,
                OccurredAtUtc = DateTimeOffset.UtcNow,
                CustomerName = Normalize(input.CustomerName),
                CustomerPhone = Normalize(input.CustomerPhone),
                PrescriptionReference = Normalize(input.PrescriptionReference),
                CashierSubject = actor,
            };

            foreach (var line in input.Items)
            {
                var medicine = medicines[line.MedicineId];

                if (!medicine.IsActive)
                {
                    throw new BusinessRuleException(
                        "sale.medicine_inactive",
                        $"'{medicine.BrandName} {medicine.Strength}' is deactivated and cannot be sold.");
                }

                var allocations = await AllocateAsync(
                    medicine.Id, line.BatchId, line.Quantity, today, cancellationToken);

                // Items for this line only, so its discount spreads across its batch splits.
                var lineItems = new List<SaleItem>();

                foreach (var (batch, quantity) in allocations)
                {
                    var item = new SaleItem
                    {
                        TenantId = tenantId,
                        MedicineId = medicine.Id,
                        BatchId = batch.Id,
                        Quantity = quantity,
                        UnitPrice = batch.SellingPrice,
                        DiscountAmount = 0,
                    };

                    sale.Items.Add(item);
                    lineItems.Add(item);

                    batch.Quantity -= quantity;

                    _dbContext.InventoryMovements.Add(new InventoryMovement
                    {
                        TenantId = tenantId,
                        MedicineId = medicine.Id,
                        BatchId = batch.Id,
                        Type = MovementTypes.Sale,
                        QuantityChange = -quantity,
                        QuantityAfter = batch.Quantity,
                        Reason = invoiceNumber,
                        ActorSubject = actor,
                        OccurredAtUtc = sale.OccurredAtUtc,
                    });
                }

                DistributeLineDiscount(lineItems, line.DiscountAmount);
            }

            var subtotal = sale.Items.Sum(item => item.Quantity * item.UnitPrice);
            var lineDiscountTotal = sale.Items.Sum(item => item.DiscountAmount);
            var discounted = subtotal - lineDiscountTotal;

            if (input.DiscountAmount < 0 || input.DiscountAmount > discounted)
            {
                throw new BusinessRuleException(
                    "sale.discount_exceeded",
                    $"Sale discount {input.DiscountAmount:F4} exceeds the discounted subtotal {discounted:F4}.");
            }

            var total = discounted - input.DiscountAmount;
            var paid = input.Payments.Sum(payment => payment.Amount);

            if (paid < total)
            {
                throw new BusinessRuleException(
                    "sale.underpaid",
                    $"Paid {paid:F4} is less than the total {total:F4}.");
            }

            sale.Subtotal = subtotal;
            sale.DiscountAmount = lineDiscountTotal + input.DiscountAmount;
            sale.TotalAmount = total;
            sale.PaidAmount = paid;

            foreach (var payment in input.Payments)
            {
                sale.Payments.Add(new SalePayment
                {
                    TenantId = tenantId,
                    Method = payment.Method,
                    Amount = payment.Amount,
                    Reference = Normalize(payment.Reference),
                    ReceivedBy = actor,
                });
            }

            _dbContext.Sales.Add(sale);
            _dbContext.SaleIdempotencyKeys.Add(new SaleIdempotencyKey
            {
                TenantId = tenantId,
                Key = key,
                SaleId = sale.Id,
            });

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                throw new ConflictException(
                    "sale.concurrent_update",
                    "Stock changed while this sale was processed. Review the cart and retry.",
                    exception);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                // Lost a race with an identical checkout: the winner stored the outcome
                // under the same key. Surface a retryable conflict; the retry replays.
                throw new ConflictException(
                    "sale.duplicate_request",
                    "This checkout is already being processed under the same key. Retry to fetch the stored sale.");
            }

            await transaction.CommitAsync(cancellationToken);

            var dto = await SaleReader.FindDtoAsync(_dbContext, sale.Id, cancellationToken);
            return new CheckoutResult(dto!, Replayed: false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Splits a line discount across its batch rows pro rata by gross, rounded to 4dp with the
    /// remainder absorbed by the last row so the parts always sum exactly. Pure function.
    /// </summary>
    private static void DistributeLineDiscount(List<SaleItem> items, decimal discount)
    {
        if (discount == 0 || items.Count == 0)
        {
            return;
        }

        var gross = items.Sum(item => item.Quantity * item.UnitPrice);
        if (discount > gross)
        {
            throw new BusinessRuleException(
                "sale.discount_exceeded",
                $"Line discount {discount:F4} exceeds its gross {gross:F4}.");
        }

        decimal assigned = 0;
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            item.DiscountAmount = index == items.Count - 1
                ? discount - assigned
                : Math.Round(
                    discount * item.Quantity * item.UnitPrice / gross,
                    4,
                    MidpointRounding.ToZero);

            assigned += item.DiscountAmount;
        }
    }

    /// <summary>
    /// FEFO allocation: earliest-expiring stocked, non-expired batches first. An explicit batch
    /// override allocates from that batch only. Shortfalls fail the whole sale (no partials).
    /// </summary>
    private async Task<List<(MedicineBatch Batch, int Quantity)>> AllocateAsync(
        Guid medicineId,
        Guid? batchId,
        int quantity,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        List<MedicineBatch> candidates;

        if (batchId.HasValue)
        {
            var forced = await _dbContext.MedicineBatches.FirstOrDefaultAsync(
                batch => batch.Id == batchId.Value && batch.MedicineId == medicineId,
                cancellationToken);

            if (forced is null || forced.Quantity < quantity || forced.ExpiryDate <= today)
            {
                throw new BusinessRuleException(
                    "sale.batch_unavailable",
                    "The selected batch cannot cover this quantity (missing, short, or expired).");
            }

            return [(forced, quantity)];
        }

        candidates = await _dbContext.MedicineBatches
            .Where(batch => batch.MedicineId == medicineId
                && batch.Quantity > 0
                && batch.ExpiryDate > today)
            .OrderBy(batch => batch.ExpiryDate)
            .ThenBy(batch => batch.BatchNumber)
            .ToListAsync(cancellationToken);

        var allocations = new List<(MedicineBatch, int)>();
        var remaining = quantity;

        foreach (var batch in candidates)
        {
            if (remaining == 0)
            {
                break;
            }

            var take = Math.Min(batch.Quantity, remaining);
            allocations.Add((batch, take));
            remaining -= take;
        }

        if (remaining > 0)
        {
            throw new BusinessRuleException(
                "sale.insufficient_stock",
                "Not enough sellable stock for the requested quantity " +
                "(expired and empty batches are never sold).");
        }

        return allocations;
    }

    private async Task EnsureCounterAsync(string tenantId, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.InvoiceCounters.AnyAsync(
            counter => counter.TenantId == tenantId,
            cancellationToken);

        if (exists)
        {
            return;
        }

        _dbContext.InvoiceCounters.Add(new InvoiceCounter { TenantId = tenantId, LastNumber = 0 });

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // A concurrent checkout created the row first; detach ours and continue.
            _dbContext.ChangeTracker.Clear();
        }
    }

    private async Task<string> NextInvoiceNumberAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        // UPDATE...RETURNING is non-composable: materialize before Single.
        var numbers = await _dbContext.Database.SqlQueryRaw<long>(
            "UPDATE invoice_counters SET \"LastNumber\" = \"LastNumber\" + 1 " +
            "WHERE \"TenantId\" = {0} RETURNING \"LastNumber\"",
            tenantId).ToListAsync(cancellationToken);

        return $"INV-{numbers.Single():000000}";
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
