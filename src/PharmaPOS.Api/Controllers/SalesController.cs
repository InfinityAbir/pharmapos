using MediatR;
using Microsoft.AspNetCore.Mvc;
using PharmaPOS.Api.Authentication;
using PharmaPOS.Application.Common.Authorization;
using PharmaPOS.Application.Common.Sales;
using PharmaPOS.Application.Features.Sales;
using PharmaPOS.Application.Features.Sales.Checkout;
using PharmaPOS.Application.Features.Sales.GetReceipt;
using PharmaPOS.Application.Features.Sales.GetSale;
using PharmaPOS.Application.Features.Sales.GetTodaySales;
using PharmaPOS.Application.Features.Sales.ListSales;

namespace PharmaPOS.Api.Controllers;

/// <summary>
/// Current-tenant sales: atomic idempotent checkout, history and receipts.
/// Controllers translate HTTP only; all behaviour lives in the application layer.
/// </summary>
[ApiController]
[Route("api/v1/tenant/sales")]
public sealed class SalesController : ControllerBase
{
    private readonly ISender _sender;

    public SalesController(ISender sender) => _sender = sender;

    /// <summary>
    /// Completes a sale atomically: invoice numbering, batch deduction (FEFO), ledger rows and
    /// payments in one transaction. Repeating the call with the same idempotency key returns
    /// the stored sale (signalled by <c>X-Idempotent-Replayed: true</c>) without charging or
    /// posting again. Prices resolve server-side; the client sends quantities, never money.
    /// </summary>
    [HasPermission(Permissions.Tenant.SalesManage)]
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(SaleDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<SaleDto>> Checkout(
        [FromBody] CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CheckoutCommand(request.ToInput()),
            cancellationToken);

        if (result.Replayed)
        {
            Response.Headers.Append("X-Idempotent-Replayed", "true");
            return Ok(result.Sale);
        }

        return StatusCode(StatusCodes.Status201Created, result.Sale);
    }

    /// <summary>Lists sales, newest first, with optional date range and paging.</summary>
    [HasPermission(Permissions.Tenant.SalesRead)]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SaleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SaleDto>>> ListSales(
        CancellationToken cancellationToken,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25)
    {
        var result = await _sender.Send(
            new ListSalesQuery(from, to, skip, take),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Gets a sale with its items and payments.</summary>
    [HasPermission(Permissions.Tenant.SalesRead)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SaleDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SaleDto>> GetSale(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSaleQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>Gets the printable receipt (pharmacy profile + sale).</summary>
    [HasPermission(Permissions.Tenant.SalesRead)]
    [HttpGet("{id:guid}/receipt")]
    [ProducesResponseType(typeof(ReceiptDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReceiptDto>> GetReceipt(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetReceiptQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns today's sales count and total for dashboards.</summary>
    [HasPermission(Permissions.Tenant.SalesRead)]
    [HttpGet("summary/today")]
    [ProducesResponseType(typeof(TodaySalesDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TodaySalesDto>> GetTodaySales(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTodaySalesQuery(), cancellationToken);
        return Ok(result);
    }
}

public sealed record CheckoutItemRequest(
    Guid MedicineId,
    int Quantity,
    Guid? BatchId,
    decimal DiscountAmount)
{
    public CheckoutItemInput ToInput() => new(MedicineId, Quantity, BatchId, DiscountAmount);
}

public sealed record CheckoutPaymentRequest(
    string Method,
    decimal Amount,
    string? Reference)
{
    public CheckoutPaymentInput ToInput() => new(Method, Amount, Reference);
}

public sealed record CheckoutRequest(
    IReadOnlyList<CheckoutItemRequest> Items,
    IReadOnlyList<CheckoutPaymentRequest> Payments,
    decimal DiscountAmount,
    Guid? CustomerId,
    string? CustomerName,
    string? CustomerPhone,
    string? PrescriptionReference,
    string IdempotencyKey)
{
    public CheckoutInput ToInput() => new(
        Items.Select(item => item.ToInput()).ToArray(),
        Payments.Select(payment => payment.ToInput()).ToArray(),
        DiscountAmount, CustomerId, CustomerName, CustomerPhone, PrescriptionReference,
        IdempotencyKey);
}
