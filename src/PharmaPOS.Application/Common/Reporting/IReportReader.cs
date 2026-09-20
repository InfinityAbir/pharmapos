using PharmaPOS.Application.Features.Inventory;
using PharmaPOS.Application.Features.Reports;

namespace PharmaPOS.Application.Common.Reporting;

/// <summary>
/// Tenant-scoped dashboard/report reads. Every method aggregates in PostgreSQL
/// (SUM/COUNT/GROUP BY over indexed, tenant-filtered sets) and returns only compact
/// DTOs: callers never receive the underlying sales, batches, or purchases.
/// </summary>
public interface IReportReader
{
    Task<IReadOnlyList<SalesTrendBucketDto>> GetSalesTrendAsync(
        DateOnly from,
        DateOnly to,
        string bucket,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesByCashierRowDto>> GetSalesByCashierAsync(
        DateOnly from,
        DateOnly to,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesByMethodRowDto>> GetSalesByMethodAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesByMedicineRowDto>> GetSalesByMedicineAsync(
        DateOnly from,
        DateOnly to,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductPerformanceRowDto>> GetProductPerformanceAsync(
        int days,
        bool bestFirst,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<StockValuationDto> GetStockValuationAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LowStockRowDto>> GetLowStockReportAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MovementDto>> GetMovementReportAsync(
        Guid? medicineId,
        Guid? batchId,
        string? type,
        DateOnly? from,
        DateOnly? to,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PurchasesBySupplierRowDto>> GetPurchasesBySupplierAsync(
        DateOnly from,
        DateOnly to,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<PurchaseTotalsDto> GetPurchaseTotalsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
