namespace PharmaPOS.Application.Features.Reports;

/// <summary>
/// One bucket of the sales trend. Periods are tenant-local days/weeks/months, computed
/// entirely in PostgreSQL; the API returns only the buckets, never the underlying sales.
/// </summary>
public sealed record SalesTrendBucketDto(
    DateOnly PeriodStart,
    int SaleCount,
    decimal TotalAmount);

public sealed record SalesByCashierRowDto(
    string Cashier,
    int SaleCount,
    decimal TotalAmount);

public sealed record SalesByMethodRowDto(
    string Method,
    int PaymentCount,
    decimal TotalAmount);

public sealed record SalesByMedicineRowDto(
    Guid MedicineId,
    string MedicineName,
    int QuantitySold,
    decimal Revenue);

/// <summary>
/// Product performance over a trailing window, rankable both ways. Slow-movers include
/// active medicines with zero sales in the window (server-side outer join).
/// </summary>
public sealed record ProductPerformanceRowDto(
    Guid MedicineId,
    string MedicineName,
    int QuantitySold,
    decimal Revenue);

public sealed record StockValuationDto(
    int MedicineCount,
    long TotalUnits,
    decimal PurchaseValue,
    decimal SellingValue,
    IReadOnlyList<StockValuationRowDto> Rows);

public sealed record StockValuationRowDto(
    Guid MedicineId,
    string MedicineName,
    int TotalQuantity,
    decimal PurchaseValue,
    decimal SellingValue);

public sealed record LowStockRowDto(
    Guid MedicineId,
    string MedicineName,
    int TotalQuantity,
    int ReorderLevel,
    int Deficit);

public sealed record PurchasesBySupplierRowDto(
    Guid SupplierId,
    string SupplierName,
    int PurchaseCount,
    decimal TotalAmount);

public sealed record PurchaseTotalsDto(
    DateOnly From,
    DateOnly To,
    int ReceivedCount,
    decimal ReceivedTotal,
    int DraftCount);
