using MediatR;
using Microsoft.AspNetCore.Mvc;
using PharmaPOS.Api.Authentication;
using PharmaPOS.Application.Common.Authorization;
using PharmaPOS.Application.Features.Inventory;
using PharmaPOS.Application.Features.Reports;
using PharmaPOS.Application.Features.Reports.GetLowStockReport;
using PharmaPOS.Application.Features.Reports.GetMovementReport;
using PharmaPOS.Application.Features.Reports.GetProductPerformance;
using PharmaPOS.Application.Features.Reports.GetPurchaseTotals;
using PharmaPOS.Application.Features.Reports.GetPurchasesBySupplier;
using PharmaPOS.Application.Features.Reports.GetSalesByCashier;
using PharmaPOS.Application.Features.Reports.GetSalesByMedicine;
using PharmaPOS.Application.Features.Reports.GetSalesByMethod;
using PharmaPOS.Application.Features.Reports.GetSalesTrend;
using PharmaPOS.Application.Features.Reports.GetStockValuation;

namespace PharmaPOS.Api.Controllers;

/// <summary>
/// Current-tenant dashboard/report reads. Every endpoint aggregates in PostgreSQL and
/// returns compact DTOs — the API never ships underlying sales, batches, or purchases
/// to the client. Controllers translate HTTP only.
/// </summary>
[ApiController]
[Route("api/v1/tenant/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly ISender _sender;

    public ReportsController(ISender sender) => _sender = sender;

    /// <summary>Sales trend buckets (day/week/month) over a bounded tenant-local range.</summary>
    [HasPermission(Permissions.Tenant.SalesRead)]
    [HttpGet("sales/trend")]
    [ProducesResponseType(typeof(IReadOnlyList<SalesTrendBucketDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SalesTrendBucketDto>>> GetSalesTrend(
        CancellationToken cancellationToken,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] string bucket = "day")
    {
        var result = await _sender.Send(
            new GetSalesTrendQuery(from, to, bucket),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Sales grouped by cashier, ranked by revenue.</summary>
    [HasPermission(Permissions.Tenant.SalesRead)]
    [HttpGet("sales/by-cashier")]
    [ProducesResponseType(typeof(IReadOnlyList<SalesByCashierRowDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SalesByCashierRowDto>>> GetSalesByCashier(
        CancellationToken cancellationToken,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25)
    {
        var result = await _sender.Send(
            new GetSalesByCashierQuery(from, to, skip, take),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Payments grouped by method (Cash/Card/bKash/Nagad/Other).</summary>
    [HasPermission(Permissions.Tenant.SalesRead)]
    [HttpGet("sales/by-method")]
    [ProducesResponseType(typeof(IReadOnlyList<SalesByMethodRowDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SalesByMethodRowDto>>> GetSalesByMethod(
        CancellationToken cancellationToken,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null)
    {
        var result = await _sender.Send(
            new GetSalesByMethodQuery(from, to),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Sales grouped by medicine, ranked by revenue.</summary>
    [HasPermission(Permissions.Tenant.SalesRead)]
    [HttpGet("sales/by-medicine")]
    [ProducesResponseType(typeof(IReadOnlyList<SalesByMedicineRowDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SalesByMedicineRowDto>>> GetSalesByMedicine(
        CancellationToken cancellationToken,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25)
    {
        var result = await _sender.Send(
            new GetSalesByMedicineQuery(from, to, skip, take),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Product performance over a trailing window: best sellers first by default,
    /// slowest first (including zero-sale actives) on request. Feeds top/slow reports
    /// and the dashboard top-sellers card.
    /// </summary>
    [HasPermission(Permissions.Tenant.SalesRead)]
    [HttpGet("products/performance")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductPerformanceRowDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductPerformanceRowDto>>> GetProductPerformance(
        CancellationToken cancellationToken,
        [FromQuery] int days = 30,
        [FromQuery] bool bestFirst = true,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25)
    {
        var result = await _sender.Send(
            new GetProductPerformanceQuery(days, bestFirst, skip, take),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Stock valuation: portfolio totals plus per-medicine rows, ranked by value.</summary>
    [HasPermission(Permissions.Tenant.InventoryRead)]
    [HttpGet("inventory/valuation")]
    [ProducesResponseType(typeof(StockValuationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StockValuationDto>> GetStockValuation(
        CancellationToken cancellationToken,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25)
    {
        var result = await _sender.Send(
            new GetStockValuationQuery(skip, take),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Low-stock report: grouped batch totals below reorder level, worst first.</summary>
    [HasPermission(Permissions.Tenant.InventoryRead)]
    [HttpGet("inventory/low-stock")]
    [ProducesResponseType(typeof(IReadOnlyList<LowStockRowDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LowStockRowDto>>> GetLowStockReport(
        CancellationToken cancellationToken,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25)
    {
        var result = await _sender.Send(
            new GetLowStockReportQuery(skip, take),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Movement ledger with type and date filters, newest first.</summary>
    [HasPermission(Permissions.Tenant.InventoryRead)]
    [HttpGet("inventory/movements")]
    [ProducesResponseType(typeof(IReadOnlyList<MovementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MovementDto>>> GetMovementReport(
        CancellationToken cancellationToken,
        [FromQuery] Guid? medicineId = null,
        [FromQuery] Guid? batchId = null,
        [FromQuery] string? type = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        var result = await _sender.Send(
            new GetMovementReportQuery(medicineId, batchId, type, from, to, skip, take),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Received purchases grouped by supplier, ranked by total.</summary>
    [HasPermission(Permissions.Tenant.PurchasingRead)]
    [HttpGet("purchases/by-supplier")]
    [ProducesResponseType(typeof(IReadOnlyList<PurchasesBySupplierRowDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PurchasesBySupplierRowDto>>> GetPurchasesBySupplier(
        CancellationToken cancellationToken,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25)
    {
        var result = await _sender.Send(
            new GetPurchasesBySupplierQuery(from, to, skip, take),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Purchase totals: received count/total over the range plus open drafts.</summary>
    [HasPermission(Permissions.Tenant.PurchasingRead)]
    [HttpGet("purchases/totals")]
    [ProducesResponseType(typeof(PurchaseTotalsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PurchaseTotalsDto>> GetPurchaseTotals(
        CancellationToken cancellationToken,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null)
    {
        var result = await _sender.Send(
            new GetPurchaseTotalsQuery(from, to),
            cancellationToken);

        return Ok(result);
    }
}
