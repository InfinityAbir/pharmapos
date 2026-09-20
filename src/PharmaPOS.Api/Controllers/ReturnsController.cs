using MediatR;
using Microsoft.AspNetCore.Mvc;
using PharmaPOS.Api.Authentication;
using PharmaPOS.Application.Common.Authorization;
using PharmaPOS.Application.Common.Sales;
using PharmaPOS.Application.Features.Returns;
using PharmaPOS.Application.Features.Returns.GetReturn;
using PharmaPOS.Application.Features.Returns.GetReturnable;
using PharmaPOS.Application.Features.Returns.ListReturns;
using PharmaPOS.Application.Features.Returns.ProcessReturn;

namespace PharmaPOS.Api.Controllers;

/// <summary>
/// Current-tenant sale returns: reversal records for immutable sales (restock + refund).
/// Controllers translate HTTP only; all behaviour lives in the application layer.
/// </summary>
[ApiController]
[Route("api/v1/tenant/returns")]
public sealed class ReturnsController : ControllerBase
{
    private readonly ISender _sender;

    public ReturnsController(ISender sender) => _sender = sender;

    /// <summary>
    /// Processes a return atomically: return record + batch restock (when requested) +
    /// SALE_RETURN movements. Repeating the call with the same idempotency key returns
    /// the stored return (signalled by <c>X-Idempotent-Replayed: true</c>) without
    /// restocking or refunding again. Refunds are computed server-side; the client sends
    /// quantities, never money.
    /// </summary>
    [HasPermission(Permissions.Tenant.ReturnsManage)]
    [HttpPost]
    [ProducesResponseType(typeof(ReturnDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ReturnDto>> ProcessReturn(
        [FromBody] ProcessReturnRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ProcessReturnCommand(request.ToInput()),
            cancellationToken);

        if (result.Replayed)
        {
            Response.Headers.Append("X-Idempotent-Replayed", "true");
            return Ok(result.Return);
        }

        return StatusCode(StatusCodes.Status201Created, result.Return);
    }

    /// <summary>Lists returns, newest first, optionally filtered by sale.</summary>
    [HasPermission(Permissions.Tenant.ReturnsRead)]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ReturnDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReturnDto>>> ListReturns(
        CancellationToken cancellationToken,
        [FromQuery] Guid? saleId = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25)
    {
        var result = await _sender.Send(
            new ListReturnsQuery(saleId, skip, take),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Gets a return with its items.</summary>
    [HasPermission(Permissions.Tenant.ReturnsRead)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ReturnDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReturnDto>> GetReturn(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetReturnQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>Gets the returnable quantities and per-unit nets for a sale.</summary>
    [HasPermission(Permissions.Tenant.ReturnsRead)]
    [HttpGet("sales/{saleId:guid}/returnable")]
    [ProducesResponseType(typeof(ReturnableSaleDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReturnableSaleDto>> GetReturnable(
        [FromRoute] Guid saleId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetReturnableQuery(saleId), cancellationToken);
        return Ok(result);
    }
}

public sealed record ReturnItemRequest(Guid SaleItemId, int Quantity)
{
    public ReturnItemInput ToInput() => new(SaleItemId, Quantity);
}

public sealed record ProcessReturnRequest(
    Guid SaleId,
    IReadOnlyList<ReturnItemRequest> Items,
    string Reason,
    bool Restock,
    string RefundMethod,
    string? RefundReference,
    string IdempotencyKey)
{
    public ReturnInput ToInput() => new(
        SaleId,
        Items.Select(item => item.ToInput()).ToArray(),
        Reason, Restock, RefundMethod, RefundReference, IdempotencyKey);
}
