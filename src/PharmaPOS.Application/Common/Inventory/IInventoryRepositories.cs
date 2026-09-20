using PharmaPOS.Application.Features.Inventory;

namespace PharmaPOS.Application.Common.Inventory;

/// <summary>
/// Tenant-scoped batch writes. Each operation updates the batch and appends exactly one ledger
/// row in the same transaction; optimistic-concurrency conflicts surface as 409.
/// </summary>
public interface IBatchRepository
{
    Task<IReadOnlyList<BatchDto>> ListAsync(
        Guid? medicineId,
        int? expiringWithinDays,
        bool includeExpired,
        CancellationToken cancellationToken = default);

    Task<BatchDto?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task<BatchDto> ReceiveAsync(
        ReceiveStockInput input,
        CancellationToken cancellationToken = default);

    Task<BatchDto> AdjustAsync(
        Guid id,
        int quantityChange,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<BatchDto> ExpireAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record ReceiveStockInput(
    Guid MedicineId,
    string BatchNumber,
    int Quantity,
    decimal? PurchasePrice,
    decimal? SellingPrice,
    DateOnly? ManufactureDate,
    DateOnly ExpiryDate,
    string? SupplierName,
    string? PurchaseReference);

/// <summary>Tenant-scoped derived stock reads (totals are summed from batches, never stored).</summary>
public interface IStockReader
{
    Task<MedicineStockDto?> GetMedicineStockAsync(
        Guid medicineId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MedicineStockDto>> ListStockAsync(
        string? search,
        bool lowStockOnly,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MedicineStockDto>> ListLowStockAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BatchDto>> ListExpiringAsync(
        int withinDays,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BatchDto>> ListExpiredAsync(
        CancellationToken cancellationToken = default);

    Task<AlertsSummaryDto> GetAlertsSummaryAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>Tenant-scoped append-only movement log (no writes by design).</summary>
public interface IMovementReader
{
    Task<IReadOnlyList<MovementDto>> ListAsync(
        Guid? medicineId,
        Guid? batchId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}
