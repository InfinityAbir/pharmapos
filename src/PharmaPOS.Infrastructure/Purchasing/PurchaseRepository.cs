using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PharmaPOS.Application.Common.Purchasing;
using PharmaPOS.Application.Features.Purchasing;
using PharmaPOS.Domain.Exceptions;
using PharmaPOS.Domain.Inventory;
using PharmaPOS.Domain.Purchasing;
using PharmaPOS.Infrastructure.Inventory;
using PharmaPOS.Infrastructure.Persistence;

namespace PharmaPOS.Infrastructure.Purchasing;

/// <summary>
/// Tenant-scoped purchase documents. Drafts are editable; receiving posts batches and
/// <c>PURCHASE_RECEIVED</c> movements atomically and is guarded by idempotency keys so retries
/// and double-clicks can never double-post stock.
/// </summary>
public sealed class PurchaseRepository : IPurchaseRepository
{
    private readonly AppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PurchaseRepository(AppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IReadOnlyList<PurchaseDto>> ListAsync(
        Guid? supplierId,
        string? status,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Purchases
            .AsNoTracking()
            .Include(purchase => purchase.Supplier)
            .AsQueryable();

        if (supplierId.HasValue)
        {
            query = query.Where(purchase => purchase.SupplierId == supplierId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(purchase => purchase.Status == status);
        }

        var purchases = await query
            .OrderByDescending(purchase => purchase.PurchaseDate)
            .ThenByDescending(purchase => purchase.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        var dtos = new List<PurchaseDto>(purchases.Count);
        foreach (var purchase in purchases)
        {
            dtos.Add(await ToDtoAsync(purchase, cancellationToken));
        }

        return dtos;
    }

    public async Task<PurchaseDto?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var purchase = await _dbContext.Purchases
            .AsNoTracking()
            .Include(candidate => candidate.Supplier)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (purchase is null)
        {
            return null;
        }

        return await ToDtoAsync(purchase, cancellationToken);
    }

    public async Task<PurchaseDto> CreateDraftAsync(
        PurchaseInput input,
        CancellationToken cancellationToken)
    {
        var supplier = await RequireSupplierAsync(input.SupplierId, cancellationToken);
        await RequireMedicinesAsync(input.Items, cancellationToken);

        var (subtotal, total) = PurchaseTotals.Calculate(
            input.Items, input.DiscountAmount, input.TaxAmount);

        var purchase = new Purchase
        {
            TenantId = TenantScope.CurrentTenantId(_dbContext),
            SupplierId = supplier.Id,
            Supplier = supplier,
            PurchaseDate = input.PurchaseDate,
            Status = PurchaseStatus.Draft,
            SupplierInvoiceNumber = Normalize(input.SupplierInvoiceNumber),
            DiscountAmount = input.DiscountAmount,
            TaxAmount = input.TaxAmount,
            TotalAmount = total,
            Notes = Normalize(input.Notes),
            Items = input.Items.Select(ToItem).ToList(),
        };

        _dbContext.Purchases.Add(purchase);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await FindAsync(purchase.Id, cancellationToken))!;
    }

    public async Task<PurchaseDto> UpdateDraftAsync(
        Guid id,
        PurchaseInput input,
        CancellationToken cancellationToken)
    {
        var purchase = await _dbContext.Purchases
            .Include(candidate => candidate.Items)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (purchase is null)
        {
            throw new NotFoundException("purchase.not_found", $"Purchase '{id}' was not found.");
        }

        RequireDraft(purchase);

        var supplier = await RequireSupplierAsync(input.SupplierId, cancellationToken);
        await RequireMedicinesAsync(input.Items, cancellationToken);

        var (subtotal, total) = PurchaseTotals.Calculate(
            input.Items, input.DiscountAmount, input.TaxAmount);

        purchase.SupplierId = supplier.Id;
        purchase.PurchaseDate = input.PurchaseDate;
        purchase.SupplierInvoiceNumber = Normalize(input.SupplierInvoiceNumber);
        purchase.DiscountAmount = input.DiscountAmount;
        purchase.TaxAmount = input.TaxAmount;
        purchase.TotalAmount = total;
        purchase.Notes = Normalize(input.Notes);

        // Replace lines wholesale (drafts have no ledger rows to preserve).
        _dbContext.PurchaseItems.RemoveRange(purchase.Items);
        purchase.Items = input.Items.Select(ToItem).ToList();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await FindAsync(purchase.Id, cancellationToken))!;
    }

    public async Task<ReceivePurchaseResult> ReceiveAsync(
        Guid id,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantScope.CurrentTenantId(_dbContext);
        idempotencyKey = idempotencyKey.Trim();

        // Replay path: the key already mapped to a purchase — return the stored outcome
        // without touching stock.
        var replay = await _dbContext.IdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Key == idempotencyKey,
                cancellationToken);

        if (replay is not null)
        {
            return new ReceivePurchaseResult(
                (await FindAsync(replay.PurchaseId, cancellationToken))!,
                Replayed: true);
        }

        var purchase = await _dbContext.Purchases
            .Include(candidate => candidate.Supplier)
            .Include(candidate => candidate.Items)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (purchase is null)
        {
            throw new NotFoundException("purchase.not_found", $"Purchase '{id}' was not found.");
        }

        if (purchase.Status != PurchaseStatus.Draft)
        {
            throw new BusinessRuleException(
                "purchase.not_draft",
                $"Only draft purchases can be received (current status: {purchase.Status}).");
        }

        var medicineIds = purchase.Items.Select(item => item.MedicineId).Distinct().ToArray();
        var medicines = await _dbContext.Medicines
            .Where(medicine => medicineIds.Contains(medicine.Id))
            .ToDictionaryAsync(medicine => medicine.Id, cancellationToken);

        if (medicines.Count != medicineIds.Length)
        {
            var missing = medicineIds.Except(medicines.Keys).ToArray();
            throw new NotFoundException(
                "medicine.not_found",
                $"Medicine(s) no longer exist in this tenant: {string.Join(", ", missing)}.");
        }

        var actor = Actor();
        foreach (var item in purchase.Items)
        {
            var medicine = medicines[item.MedicineId];

            var batch = await StockIntake.ResolveForReceiveAsync(
                _dbContext,
                medicine,
                item.BatchNumber,
                item.UnitPrice,
                item.SellingPrice,
                item.ManufactureDate,
                item.ExpiryDate,
                purchase.Supplier.Name,
                purchase.SupplierInvoiceNumber,
                cancellationToken);

            batch.Quantity += item.Quantity;

            _dbContext.InventoryMovements.Add(StockIntake.BuildMovement(
                tenantId,
                medicine.Id,
                batch,
                MovementTypes.PurchaseReceived,
                item.Quantity,
                batch.Quantity,
                purchase.SupplierInvoiceNumber,
                actor));
        }

        var (subtotal, total) = PurchaseTotals.Calculate(
            purchase.Items.Select(ToInput).ToArray(),
            purchase.DiscountAmount,
            purchase.TaxAmount);

        purchase.TotalAmount = total;
        purchase.Status = PurchaseStatus.Received;
        purchase.ReceivedAtUtc = DateTimeOffset.UtcNow;

        _dbContext.IdempotencyKeys.Add(new IdempotencyKey
        {
            TenantId = tenantId,
            Key = idempotencyKey.Trim(),
            PurchaseId = purchase.Id,
        });

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConflictException(
                "purchase.concurrent_update",
                "This purchase changed while your request was processed. Reload and retry.",
                exception);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // Lost a race with an identical receive: the winner stored the outcome under
            // the same key. Surface a retryable conflict; the retry replays the purchase.
            throw new ConflictException(
                "purchase.duplicate_request",
                "This receive is already being processed under the same key. Retry to fetch the stored outcome.");
        }

        return new ReceivePurchaseResult(
            (await FindAsync(purchase.Id, cancellationToken))!,
            Replayed: false);
    }

    public async Task<PurchaseDto> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var purchase = await _dbContext.Purchases
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (purchase is null)
        {
            throw new NotFoundException("purchase.not_found", $"Purchase '{id}' was not found.");
        }

        RequireDraft(purchase);

        purchase.Status = PurchaseStatus.Cancelled;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await FindAsync(purchase.Id, cancellationToken))!;
    }

    public async Task<PurchasingSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var monthStart = new DateOnly(
            DateTime.UtcNow.Year, DateTime.UtcNow.Month, day: 1);

        var drafts = await _dbContext.Purchases.CountAsync(
            purchase => purchase.Status == PurchaseStatus.Draft,
            cancellationToken);

        // Dashboard metric: COUNT/SUM in PostgreSQL over the month slice.
        // Only scalars leave the database, never the month's purchase rows.
        var received = _dbContext.Purchases
            .AsNoTracking()
            .Where(purchase => purchase.Status == PurchaseStatus.Received
                && purchase.PurchaseDate >= monthStart);

        return new PurchasingSummaryDto(
            DraftCount: drafts,
            ReceivedThisMonthCount: await received.CountAsync(cancellationToken),
            ReceivedThisMonthTotal: await received.SumAsync(
                purchase => (decimal?)purchase.TotalAmount, cancellationToken) ?? 0m);
    }

    private async Task<Supplier> RequireSupplierAsync(
        Guid supplierId,
        CancellationToken cancellationToken)
    {
        var supplier = await _dbContext.Suppliers
            .FirstOrDefaultAsync(candidate => candidate.Id == supplierId, cancellationToken);

        if (supplier is null)
        {
            throw new NotFoundException(
                "supplier.not_found",
                $"Supplier '{supplierId}' was not found in this tenant.");
        }

        return supplier;
    }

    private async Task RequireMedicinesAsync(
        IReadOnlyList<PurchaseItemInput> items,
        CancellationToken cancellationToken)
    {
        var ids = items.Select(item => item.MedicineId).Distinct().ToArray();
        var count = await _dbContext.Medicines.CountAsync(
            medicine => ids.Contains(medicine.Id),
            cancellationToken);

        if (count != ids.Length)
        {
            throw new NotFoundException(
                "medicine.not_found",
                "One or more medicines do not exist in this tenant.");
        }
    }

    private static void RequireDraft(Purchase purchase)
    {
        if (purchase.Status != PurchaseStatus.Draft)
        {
            throw new BusinessRuleException(
                "purchase.not_draft",
                $"Only draft purchases can be changed (current status: {purchase.Status}).");
        }
    }

    private async Task<PurchaseDto> ToDtoAsync(
        Purchase purchase,
        CancellationToken cancellationToken)
    {
        var items = await _dbContext.PurchaseItems
            .AsNoTracking()
            .Include(item => item.Medicine)
            .Where(item => item.PurchaseId == purchase.Id)
            .OrderBy(item => item.Medicine.BrandName)
            .ThenBy(item => item.BatchNumber)
            .ToListAsync(cancellationToken);

        var (subtotal, total) = PurchaseTotals.Calculate(
            items.Select(ToInput).ToArray(),
            purchase.DiscountAmount,
            purchase.TaxAmount);

        return new PurchaseDto(
            Id: purchase.Id,
            SupplierId: purchase.SupplierId,
            SupplierName: purchase.Supplier?.Name ?? purchase.SupplierId.ToString(),
            PurchaseDate: purchase.PurchaseDate,
            Status: purchase.Status,
            SupplierInvoiceNumber: purchase.SupplierInvoiceNumber,
            Subtotal: subtotal,
            DiscountAmount: purchase.DiscountAmount,
            TaxAmount: purchase.TaxAmount,
            TotalAmount: total,
            Notes: purchase.Notes,
            CreatedAtUtc: purchase.CreatedAtUtc,
            ReceivedAtUtc: purchase.ReceivedAtUtc,
            Items: items.Select(item => new PurchaseItemDto(
                Id: item.Id,
                MedicineId: item.MedicineId,
                MedicineName: item.Medicine is null
                    ? item.MedicineId.ToString()
                    : $"{item.Medicine.BrandName} {item.Medicine.Strength}",
                BatchNumber: item.BatchNumber,
                ManufactureDate: item.ManufactureDate,
                ExpiryDate: item.ExpiryDate,
                Quantity: item.Quantity,
                UnitPrice: item.UnitPrice,
                DiscountAmount: item.DiscountAmount,
                SellingPrice: item.SellingPrice,
                LineTotal: item.Quantity * item.UnitPrice - item.DiscountAmount)).ToArray());
    }

    private static PurchaseItem ToItem(PurchaseItemInput input)
        => new()
        {
            MedicineId = input.MedicineId,
            BatchNumber = input.BatchNumber.Trim(),
            ManufactureDate = input.ManufactureDate,
            ExpiryDate = input.ExpiryDate,
            Quantity = input.Quantity,
            UnitPrice = input.UnitPrice,
            DiscountAmount = input.DiscountAmount,
            SellingPrice = input.SellingPrice,
        };

    private static PurchaseItemInput ToInput(PurchaseItem item)
        => new(
            MedicineId: item.MedicineId,
            BatchNumber: item.BatchNumber,
            ManufactureDate: item.ManufactureDate,
            ExpiryDate: item.ExpiryDate,
            Quantity: item.Quantity,
            UnitPrice: item.UnitPrice,
            DiscountAmount: item.DiscountAmount,
            SellingPrice: item.SellingPrice);

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
