using MediatR;
using Microsoft.AspNetCore.Mvc;
using PharmaPOS.Api.Authentication;
using PharmaPOS.Application.Common.Authorization;
using PharmaPOS.Application.Common.Purchasing;
using PharmaPOS.Application.Features.Purchasing;
using PharmaPOS.Application.Features.Purchasing.CancelPurchase;
using PharmaPOS.Application.Features.Purchasing.CreateDraft;
using PharmaPOS.Application.Features.Purchasing.CreateSupplier;
using PharmaPOS.Application.Features.Purchasing.DeleteSupplier;
using PharmaPOS.Application.Features.Purchasing.GetPurchase;
using PharmaPOS.Application.Features.Purchasing.GetPurchasingSummary;
using PharmaPOS.Application.Features.Purchasing.GetSupplier;
using PharmaPOS.Application.Features.Purchasing.ListPurchases;
using PharmaPOS.Application.Features.Purchasing.ListSuppliers;
using PharmaPOS.Application.Features.Purchasing.ReceivePurchase;
using PharmaPOS.Application.Features.Purchasing.SetSupplierStatus;
using PharmaPOS.Application.Features.Purchasing.UpdateDraft;
using PharmaPOS.Application.Features.Purchasing.UpdateSupplier;

namespace PharmaPOS.Api.Controllers;

/// <summary>
/// Current-tenant suppliers and purchase documents. Receiving posts batches and ledger rows
/// atomically and is idempotent per key. Controllers translate HTTP only.
/// </summary>
[ApiController]
[Route("api/v1/tenant")]
public sealed class PurchasingController : ControllerBase
{
    private readonly ISender _sender;

    public PurchasingController(ISender sender) => _sender = sender;

    /// <summary>Lists suppliers with optional search.</summary>
    [HasPermission(Permissions.Tenant.PurchasingRead)]
    [HttpGet("suppliers")]
    [ProducesResponseType(typeof(IReadOnlyList<SupplierDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SupplierDto>>> ListSuppliers(
        CancellationToken cancellationToken,
        [FromQuery] string? search = null)
    {
        var result = await _sender.Send(new ListSuppliersQuery(search), cancellationToken);
        return Ok(result);
    }

    /// <summary>Gets a single supplier.</summary>
    [HasPermission(Permissions.Tenant.PurchasingRead)]
    [HttpGet("suppliers/{id:guid}")]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SupplierDto>> GetSupplier(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSupplierQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>Creates a supplier.</summary>
    [HasPermission(Permissions.Tenant.PurchasingManage)]
    [HttpPost("suppliers")]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<SupplierDto>> CreateSupplier(
        [FromBody] SupplierRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreateSupplierCommand(request.ToInput()),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Updates a supplier.</summary>
    [HasPermission(Permissions.Tenant.PurchasingManage)]
    [HttpPut("suppliers/{id:guid}")]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SupplierDto>> UpdateSupplier(
        [FromRoute] Guid id,
        [FromBody] SupplierRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateSupplierCommand(id, request.ToInput()),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Activates or deactivates a supplier.</summary>
    [HasPermission(Permissions.Tenant.PurchasingManage)]
    [HttpPatch("suppliers/{id:guid}/status")]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SupplierDto>> SetSupplierStatus(
        [FromRoute] Guid id,
        [FromBody] SetSupplierStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new SetSupplierStatusCommand(id, request.IsActive),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Deletes a supplier (refused while purchases reference it).</summary>
    [HasPermission(Permissions.Tenant.PurchasingManage)]
    [HttpDelete("suppliers/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteSupplier(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteSupplierCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>Lists purchases with optional supplier/status filters and paging.</summary>
    [HasPermission(Permissions.Tenant.PurchasingRead)]
    [HttpGet("purchases")]
    [ProducesResponseType(typeof(IReadOnlyList<PurchaseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PurchaseDto>>> ListPurchases(
        CancellationToken cancellationToken,
        [FromQuery] Guid? supplierId = null,
        [FromQuery] string? status = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25)
    {
        var result = await _sender.Send(
            new ListPurchasesQuery(supplierId, status, skip, take),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Gets a purchase with its items and server-computed totals.</summary>
    [HasPermission(Permissions.Tenant.PurchasingRead)]
    [HttpGet("purchases/{id:guid}")]
    [ProducesResponseType(typeof(PurchaseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PurchaseDto>> GetPurchase(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetPurchaseQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>Creates a draft purchase (no stock moves until received).</summary>
    [HasPermission(Permissions.Tenant.PurchasingManage)]
    [HttpPost("purchases")]
    [ProducesResponseType(typeof(PurchaseDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<PurchaseDto>> CreateDraft(
        [FromBody] PurchaseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreateDraftCommand(request.ToInput()),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Updates a draft purchase (received/cancelled documents are immutable).</summary>
    [HasPermission(Permissions.Tenant.PurchasingManage)]
    [HttpPut("purchases/{id:guid}")]
    [ProducesResponseType(typeof(PurchaseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PurchaseDto>> UpdateDraft(
        [FromRoute] Guid id,
        [FromBody] PurchaseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateDraftCommand(id, request.ToInput()),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Receives a draft: posts batches and ledger rows atomically. Repeating the call with the
    /// same idempotency key returns the stored purchase (signalled by
    /// <c>X-Idempotent-Replayed: true</c>) without moving stock again.
    /// </summary>
    [HasPermission(Permissions.Tenant.PurchasingManage)]
    [HttpPost("purchases/{id:guid}/receive")]
    [ProducesResponseType(typeof(PurchaseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PurchaseDto>> ReceivePurchase(
        [FromRoute] Guid id,
        [FromBody] ReceivePurchaseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ReceivePurchaseCommand(id, request.IdempotencyKey),
            cancellationToken);

        if (result.Replayed)
        {
            Response.Headers.Append("X-Idempotent-Replayed", "true");
        }

        return Ok(result.Purchase);
    }

    /// <summary>Cancels a draft purchase.</summary>
    [HasPermission(Permissions.Tenant.PurchasingManage)]
    [HttpPost("purchases/{id:guid}/cancel")]
    [ProducesResponseType(typeof(PurchaseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PurchaseDto>> CancelPurchase(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CancelPurchaseCommand(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns purchasing totals for dashboards.</summary>
    [HasPermission(Permissions.Tenant.PurchasingRead)]
    [HttpGet("purchasing/summary")]
    [ProducesResponseType(typeof(PurchasingSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PurchasingSummaryDto>> GetPurchasingSummary(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetPurchasingSummaryQuery(), cancellationToken);
        return Ok(result);
    }
}

public sealed record SupplierRequest(
    string Name,
    string? Company,
    string? Phone,
    string? Email,
    string? Address,
    string? ContactPerson,
    string? PaymentTerms)
{
    public SupplierInput ToInput() => new(
        Name, Company, Phone, Email, Address, ContactPerson, PaymentTerms);
}

public sealed record SetSupplierStatusRequest(bool IsActive);

public sealed record PurchaseItemRequest(
    Guid MedicineId,
    string BatchNumber,
    DateOnly? ManufactureDate,
    DateOnly ExpiryDate,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal? SellingPrice)
{
    public PurchaseItemInput ToInput() => new(
        MedicineId, BatchNumber, ManufactureDate, ExpiryDate,
        Quantity, UnitPrice, DiscountAmount, SellingPrice);
}

public sealed record PurchaseRequest(
    Guid SupplierId,
    DateOnly PurchaseDate,
    string? SupplierInvoiceNumber,
    decimal DiscountAmount,
    decimal TaxAmount,
    string? Notes,
    IReadOnlyList<PurchaseItemRequest> Items)
{
    public PurchaseInput ToInput() => new(
        SupplierId, PurchaseDate, SupplierInvoiceNumber, DiscountAmount, TaxAmount, Notes,
        Items.Select(item => item.ToInput()).ToArray());
}

public sealed record ReceivePurchaseRequest(string IdempotencyKey);
