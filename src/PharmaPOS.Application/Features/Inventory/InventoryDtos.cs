namespace PharmaPOS.Application.Features.Inventory;

public sealed record BatchDto(
    Guid Id,
    Guid MedicineId,
    string MedicineName,
    string BatchNumber,
    decimal PurchasePrice,
    decimal SellingPrice,
    int Quantity,
    DateOnly? ManufactureDate,
    DateOnly ExpiryDate,
    string? SupplierName,
    string? PurchaseReference,
    bool IsExpired);

public sealed record MedicineStockDto(
    Guid MedicineId,
    string BrandName,
    string Strength,
    int TotalQuantity,
    int ReorderLevel,
    bool IsLowStock,
    int BatchCount,
    IReadOnlyList<BatchDto> Batches);

public sealed record MovementDto(
    Guid Id,
    Guid MedicineId,
    string MedicineName,
    Guid BatchId,
    string BatchNumber,
    string Type,
    int QuantityChange,
    int QuantityAfter,
    string? Reason,
    string? ActorSubject,
    DateTimeOffset OccurredAtUtc);

public sealed record AlertsSummaryDto(
    int LowStockCount,
    int ExpiringWithin30DaysCount,
    int ExpiredBatchCount);
