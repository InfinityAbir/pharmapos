using MediatR;
using Microsoft.AspNetCore.Mvc;
using PharmaPOS.Api.Authentication;
using PharmaPOS.Application.Common.Authorization;
using PharmaPOS.Application.Common.Customers;
using PharmaPOS.Application.Features.Customers;
using PharmaPOS.Application.Features.Customers.CreateCustomer;
using PharmaPOS.Application.Features.Customers.DeleteCustomer;
using PharmaPOS.Application.Features.Customers.GetCustomer;
using PharmaPOS.Application.Features.Customers.ListCustomers;
using PharmaPOS.Application.Features.Customers.SetCustomerStatus;
using PharmaPOS.Application.Features.Customers.UpdateCustomer;

namespace PharmaPOS.Api.Controllers;

/// <summary>
/// Current-tenant customers: quick creation, search and purchase history.
/// Customer info stays optional for cash sales. Controllers translate HTTP only.
/// </summary>
[ApiController]
[Route("api/v1/tenant/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly ISender _sender;

    public CustomersController(ISender sender) => _sender = sender;

    /// <summary>Lists customers with optional search, newest names first, with paging.</summary>
    [HasPermission(Permissions.Tenant.CustomersRead)]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CustomerDto>>> ListCustomers(
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25)
    {
        var result = await _sender.Send(
            new ListCustomersQuery(search, isActive, skip, take),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Gets a customer with recent sales history.</summary>
    [HasPermission(Permissions.Tenant.CustomersRead)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerWithHistoryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerWithHistoryDto>> GetCustomer(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCustomerQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>Creates a customer (quick creation from POS or Customers page).</summary>
    [HasPermission(Permissions.Tenant.CustomersManage)]
    [HttpPost]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CustomerDto>> CreateCustomer(
        [FromBody] CustomerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreateCustomerCommand(request.ToInput()),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Updates a customer.</summary>
    [HasPermission(Permissions.Tenant.CustomersManage)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerDto>> UpdateCustomer(
        [FromRoute] Guid id,
        [FromBody] CustomerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateCustomerCommand(id, request.ToInput()),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Activates or deactivates a customer.</summary>
    [HasPermission(Permissions.Tenant.CustomersManage)]
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerDto>> SetCustomerStatus(
        [FromRoute] Guid id,
        [FromBody] SetCustomerStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new SetCustomerStatusCommand(id, request.IsActive),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Deletes a customer (refused while sales reference it).</summary>
    [HasPermission(Permissions.Tenant.CustomersManage)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteCustomer(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteCustomerCommand(id), cancellationToken);
        return NoContent();
    }
}

public sealed record CustomerRequest(
    string Name,
    string? Phone,
    string? Email,
    string? Address,
    string? Notes)
{
    public CustomerInput ToInput() => new(Name, Phone, Email, Address, Notes);
}

public sealed record SetCustomerStatusRequest(bool IsActive);
