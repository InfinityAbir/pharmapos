using MediatR;
using Microsoft.AspNetCore.Mvc;
using PharmaPOS.Api.Authentication;
using PharmaPOS.Application.Common.Authorization;
using PharmaPOS.Application.Common.Inventory;
using PharmaPOS.Application.Features.Inventory;
using PharmaPOS.Application.Features.Inventory.AdjustStock;
using PharmaPOS.Application.Features.Inventory.ExpireBatch;
using PharmaPOS.Application.Features.Inventory.GetAlertsSummary;
using PharmaPOS.Application.Features.Inventory.GetMedicineStock;
using PharmaPOS.Application.Features.Inventory.ListBatches;
using PharmaPOS.Application.Features.Inventory.ListExpired;
using PharmaPOS.Application.Features.Inventory.ListExpiring;
using PharmaPOS.Application.Features.Inventory.ListLowStock;
using PharmaPOS.Application.Features.Inventory.ListMovements;
using PharmaPOS.Application.Features.Inventory.ListStock;
using PharmaPOS.Application.Features.Inventory.ReceiveStock;

namespace PharmaPOS.Api.Controllers;

/// <summary>
/// Current-tenant inventory: batch-level stock, derived stock levels, expiry/low-stock alerts
/// and the append-only movement ledger. Stock is summed from batches on every read.
/// Controllers translate HTTP only; all behaviour lives in the application layer.
/// </summary>
[ApiController]
[Route("api/v1/tenant/inventory")]
public sealed class InventoryController : ControllerBase
{
    private readonly ISender _sender;

    public InventoryController(ISender sender) => _sender = sender;

    /// <summary>Lists batches, optionally filtered by medicine or expiring horizon.</summary>
    [HasPermission(Permissions.Tenant.InventoryRead)]
    [HttpGet("batches")]
    [ProducesResponseType(typeof(IReadOnlyList<BatchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BatchDto>>> ListBatches(
        CancellationToken cancellationToken,
        [FromQuery] Guid? medicineId = null,
        [FromQuery] int? expiringWithinDays = null,
        [FromQuery] bool includeExpired = true)
    {
        var result = await _sender.Send(
            new ListBatchesQuery(medicineId, expiringWithinDays, includeExpired),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Receives stock into a new or existing batch (matched by medicine + batch number).
    /// Past expiries are rejected.
    /// </summary>
    [HasPermission(Permissions.Tenant.InventoryManage)]
    [HttpPost("batches/receive")]
    [ProducesResponseType(typeof(BatchDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<BatchDto>> ReceiveStock(
        [FromBody] ReceiveStockRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ReceiveStockCommand(request.ToInput()),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Adjusts a batch quantity (signed change + reason; never below zero).</summary>
    [HasPermission(Permissions.Tenant.InventoryManage)]
    [HttpPost("batches/{id:guid}/adjust")]
    [ProducesResponseType(typeof(BatchDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BatchDto>> AdjustStock(
        [FromRoute] Guid id,
        [FromBody] AdjustStockRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new AdjustStockCommand(id, request.QuantityChange, request.Reason),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Expires a batch: quantity to zero with an EXPIRED ledger row.</summary>
    [HasPermission(Permissions.Tenant.InventoryManage)]
    [HttpPost("batches/{id:guid}/expire")]
    [ProducesResponseType(typeof(BatchDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BatchDto>> ExpireBatch(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ExpireBatchCommand(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>Lists per-medicine stock levels with optional search and low-stock filter.</summary>
    [HasPermission(Permissions.Tenant.InventoryRead)]
    [HttpGet("stock")]
    [ProducesResponseType(typeof(IReadOnlyList<MedicineStockDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MedicineStockDto>>> ListStock(
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] bool lowStockOnly = false)
    {
        var result = await _sender.Send(
            new ListStockQuery(search, lowStockOnly),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Lists medicines whose total stock is below their reorder level.</summary>
    [HasPermission(Permissions.Tenant.InventoryRead)]
    [HttpGet("stock/low-stock")]
    [ProducesResponseType(typeof(IReadOnlyList<MedicineStockDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MedicineStockDto>>> ListLowStock(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListLowStockQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Gets one medicine's stock detail with its batches.</summary>
    [HasPermission(Permissions.Tenant.InventoryRead)]
    [HttpGet("medicines/{medicineId:guid}/stock")]
    [ProducesResponseType(typeof(MedicineStockDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MedicineStockDto>> GetMedicineStock(
        [FromRoute] Guid medicineId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMedicineStockQuery(medicineId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Lists stocked batches expiring within the horizon (default 30 days).</summary>
    [HasPermission(Permissions.Tenant.InventoryRead)]
    [HttpGet("expiring")]
    [ProducesResponseType(typeof(IReadOnlyList<BatchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BatchDto>>> ListExpiring(
        CancellationToken cancellationToken,
        [FromQuery] int withinDays = 30)
    {
        var result = await _sender.Send(new ListExpiringQuery(withinDays), cancellationToken);
        return Ok(result);
    }

    /// <summary>Lists stocked batches past their expiry date.</summary>
    [HasPermission(Permissions.Tenant.InventoryRead)]
    [HttpGet("expired")]
    [ProducesResponseType(typeof(IReadOnlyList<BatchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BatchDto>>> ListExpired(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListExpiredQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns alert counts for dashboards and badges.</summary>
    [HasPermission(Permissions.Tenant.InventoryRead)]
    [HttpGet("alerts/summary")]
    [ProducesResponseType(typeof(AlertsSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AlertsSummaryDto>> GetAlertsSummary(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetAlertsSummaryQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Reads the movement ledger, newest first.</summary>
    [HasPermission(Permissions.Tenant.InventoryRead)]
    [HttpGet("movements")]
    [ProducesResponseType(typeof(IReadOnlyList<MovementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MovementDto>>> ListMovements(
        CancellationToken cancellationToken,
        [FromQuery] Guid? medicineId = null,
        [FromQuery] Guid? batchId = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        var result = await _sender.Send(
            new ListMovementsQuery(medicineId, batchId, skip, take),
            cancellationToken);

        return Ok(result);
    }
}

public sealed record ReceiveStockRequest(
    Guid MedicineId,
    string BatchNumber,
    int Quantity,
    decimal? PurchasePrice,
    decimal? SellingPrice,
    DateOnly? ManufactureDate,
    DateOnly ExpiryDate,
    string? SupplierName,
    string? PurchaseReference)
{
    public ReceiveStockInput ToInput() => new(
        MedicineId, BatchNumber, Quantity, PurchasePrice, SellingPrice,
        ManufactureDate, ExpiryDate, SupplierName, PurchaseReference);
}

public sealed record AdjustStockRequest(int QuantityChange, string? Reason);
