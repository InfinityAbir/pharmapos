using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PharmaPOS.Application.Common.Inventory;
using PharmaPOS.Application.Features.Inventory;
using PharmaPOS.Domain.Exceptions;
using PharmaPOS.Domain.Inventory;
using PharmaPOS.Infrastructure.Persistence;

namespace PharmaPOS.Infrastructure.Inventory;

/// <summary>
/// Tenant-scoped batch writes. Every mutation updates the batch and appends exactly one ledger
/// row in the same <c>SaveChanges</c>; optimistic-concurrency conflicts surface as 409.
/// </summary>
public sealed class BatchRepository : IBatchRepository
{
    private readonly AppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BatchRepository(AppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IReadOnlyList<BatchDto>> ListAsync(
        Guid? medicineId,
        int? expiringWithinDays,
        bool includeExpired,
        CancellationToken cancellationToken)
    {
        var today = Today();
        var query = _dbContext.MedicineBatches
            .AsNoTracking()
            .Include(batch => batch.Medicine)
            .AsQueryable();

        if (medicineId.HasValue)
        {
            query = query.Where(batch => batch.MedicineId == medicineId.Value);
        }

        if (!includeExpired)
        {
            query = query.Where(batch => batch.ExpiryDate > today);
        }

        if (expiringWithinDays.HasValue)
        {
            var horizon = today.AddDays(expiringWithinDays.Value);
            query = query.Where(batch => batch.ExpiryDate > today && batch.ExpiryDate <= horizon);
        }

        var batches = await query
            .OrderBy(batch => batch.ExpiryDate)
            .ThenBy(batch => batch.BatchNumber)
            .ToListAsync(cancellationToken);

        return batches.Select(batch => ToDto(batch, today)).ToArray();
    }

    public async Task<BatchDto?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var batch = await _dbContext.MedicineBatches
            .AsNoTracking()
            .Include(candidate => candidate.Medicine)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return batch is null ? null : ToDto(batch, Today());
    }

    public async Task<BatchDto> ReceiveAsync(
        ReceiveStockInput input,
        CancellationToken cancellationToken)
    {
        var medicine = await _dbContext.Medicines
            .FirstOrDefaultAsync(candidate => candidate.Id == input.MedicineId, cancellationToken);

        if (medicine is null)
        {
            throw new NotFoundException(
                "medicine.not_found",
                $"Medicine '{input.MedicineId}' was not found.");
        }

        var batchNumber = input.BatchNumber.Trim();
        var batch = await _dbContext.MedicineBatches
            .Include(candidate => candidate.Medicine)
            .FirstOrDefaultAsync(
                candidate => candidate.MedicineId == input.MedicineId
                    && candidate.BatchNumber == batchNumber,
                cancellationToken);

        if (batch is null)
        {
            batch = new MedicineBatch
            {
                TenantId = TenantScope.CurrentTenantId(_dbContext),
                MedicineId = medicine.Id,
                Medicine = medicine,
                BatchNumber = batchNumber,
                PurchasePrice = input.PurchasePrice ?? medicine.PurchasePrice,
                SellingPrice = input.SellingPrice ?? medicine.SellingPrice,
                Quantity = 0,
                ManufactureDate = input.ManufactureDate,
                ExpiryDate = input.ExpiryDate,
                SupplierName = Normalize(input.SupplierName),
                PurchaseReference = Normalize(input.PurchaseReference),
            };

            _dbContext.MedicineBatches.Add(batch);
        }
        else
        {
            // Topping up an existing lot: dates must match (a mismatch signals the wrong
            // batch number); prices follow the latest explicit values.
            if ((input.ManufactureDate.HasValue && input.ManufactureDate != batch.ManufactureDate)
                || input.ExpiryDate != batch.ExpiryDate)
            {
                throw new BusinessRuleException(
                    "batch.date_mismatch",
                    $"Batch '{batchNumber}' already exists with different dates. " +
                    "Receive into a new batch number instead.");
            }

            if (input.PurchasePrice.HasValue)
            {
                batch.PurchasePrice = input.PurchasePrice.Value;
            }

            if (input.SellingPrice.HasValue)
            {
                batch.SellingPrice = input.SellingPrice.Value;
            }
        }

        batch.Quantity += input.Quantity;

        _dbContext.InventoryMovements.Add(new InventoryMovement
        {
            TenantId = batch.TenantId,
            MedicineId = medicine.Id,
            BatchId = batch.Id,
            Type = MovementTypes.PurchaseReceived,
            QuantityChange = input.Quantity,
            QuantityAfter = batch.Quantity,
            Reason = Normalize(input.PurchaseReference) ?? Normalize(input.SupplierName),
            ActorSubject = Actor(),
            OccurredAtUtc = DateTimeOffset.UtcNow,
        });

        await SaveAsync(cancellationToken);

        return ToDto(batch, Today());
    }

    public async Task<BatchDto> AdjustAsync(
        Guid id,
        int quantityChange,
        string? reason,
        CancellationToken cancellationToken)
    {
        var batch = await LoadAsync(id, cancellationToken);

        var resulting = batch.Quantity + quantityChange;
        if (resulting < 0)
        {
            throw new BusinessRuleException(
                "stock.insufficient",
                $"Adjustment of {quantityChange} would drive batch '{batch.BatchNumber}' " +
                $"below zero (on hand: {batch.Quantity}).");
        }

        batch.Quantity = resulting;

        _dbContext.InventoryMovements.Add(new InventoryMovement
        {
            TenantId = batch.TenantId,
            MedicineId = batch.MedicineId,
            BatchId = batch.Id,
            Type = quantityChange > 0 ? MovementTypes.AdjustmentIn : MovementTypes.AdjustmentOut,
            QuantityChange = quantityChange,
            QuantityAfter = resulting,
            Reason = Normalize(reason),
            ActorSubject = Actor(),
            OccurredAtUtc = DateTimeOffset.UtcNow,
        });

        await SaveAsync(cancellationToken);

        return ToDto(batch, Today());
    }

    public async Task<BatchDto> ExpireAsync(Guid id, CancellationToken cancellationToken)
    {
        var batch = await LoadAsync(id, cancellationToken);

        if (batch.Quantity == 0)
        {
            throw new BusinessRuleException(
                "batch.already_depleted",
                $"Batch '{batch.BatchNumber}' holds no stock to expire.");
        }

        var removed = batch.Quantity;
        batch.Quantity = 0;

        _dbContext.InventoryMovements.Add(new InventoryMovement
        {
            TenantId = batch.TenantId,
            MedicineId = batch.MedicineId,
            BatchId = batch.Id,
            Type = MovementTypes.Expired,
            QuantityChange = -removed,
            QuantityAfter = 0,
            Reason = null,
            ActorSubject = Actor(),
            OccurredAtUtc = DateTimeOffset.UtcNow,
        });

        await SaveAsync(cancellationToken);

        return ToDto(batch, Today());
    }

    private async Task<MedicineBatch> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var batch = await _dbContext.MedicineBatches
            .Include(candidate => candidate.Medicine)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (batch is null)
        {
            throw new NotFoundException("batch.not_found", $"Batch '{id}' was not found.");
        }

        return batch;
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConflictException(
                "batch.concurrent_update",
                "This batch changed while your request was processed. Reload and retry.",
                exception);
        }
    }

    private string? Actor()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.FindFirst("sub")?.Value ?? user?.FindFirst("preferred_username")?.Value;
    }

    private DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow.Date);

    private static BatchDto ToDto(MedicineBatch batch, DateOnly today)
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

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
