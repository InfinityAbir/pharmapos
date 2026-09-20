using Microsoft.EntityFrameworkCore;
using PharmaPOS.Application.Common.Inventory;
using PharmaPOS.Application.Features.Inventory;
using PharmaPOS.Infrastructure.Persistence;

namespace PharmaPOS.Infrastructure.Inventory;

/// <summary>Tenant-scoped append-only movement log (reads only, newest first).</summary>
public sealed class MovementReader : IMovementReader
{
    private readonly AppDbContext _dbContext;

    public MovementReader(AppDbContext dbContext)
        => _dbContext = dbContext;

    public async Task<IReadOnlyList<MovementDto>> ListAsync(
        Guid? medicineId,
        Guid? batchId,
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

        var movements = await query
            .OrderByDescending(movement => movement.OccurredAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return movements.Select(movement => new MovementDto(
            Id: movement.Id,
            MedicineId: movement.MedicineId,
            MedicineName: movement.Medicine is null
                ? movement.MedicineId.ToString()
                : $"{movement.Medicine.BrandName} {movement.Medicine.Strength}",
            BatchId: movement.BatchId,
            BatchNumber: movement.Batch?.BatchNumber ?? movement.BatchId.ToString(),
            Type: movement.Type,
            QuantityChange: movement.QuantityChange,
            QuantityAfter: movement.QuantityAfter,
            Reason: movement.Reason,
            ActorSubject: movement.ActorSubject,
            OccurredAtUtc: movement.OccurredAtUtc)).ToArray();
    }
}
