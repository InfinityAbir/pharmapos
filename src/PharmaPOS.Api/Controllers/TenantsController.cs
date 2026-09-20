using MediatR;
using Microsoft.AspNetCore.Mvc;
using PharmaPOS.Api.Authentication;
using PharmaPOS.Application.Common.Authorization;
using PharmaPOS.Application.Features.Tenancy;
using PharmaPOS.Application.Features.Tenancy.CreateTenant;
using PharmaPOS.Application.Features.Tenancy.ListTenants;
using PharmaPOS.Application.Features.Tenancy.SetTenantStatus;

namespace PharmaPOS.Api.Controllers;

/// <summary>
/// Platform-level tenant administration. Guarded by <c>platform.tenants.manage</c>; the tenant
/// directory is platform-wide and deliberately not scoped to the caller's tenant.
/// Controllers translate HTTP only; all behaviour lives in the application layer.
/// </summary>
[ApiController]
[Route("api/v1/platform/tenants")]
[HasPermission(Permissions.Platform.TenantsManage)]
public sealed class TenantsController : ControllerBase
{
    private readonly ISender _sender;

    public TenantsController(ISender sender) => _sender = sender;

    /// <summary>Lists all tenants in the platform directory.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TenantDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TenantDto>>> List(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListTenantsQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Creates a tenant. Identifier must be unique across the platform.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TenantDto>> Create(
        [FromBody] CreateTenantRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreateTenantCommand(request.Identifier, request.Name),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Sets a tenant's lifecycle status (<c>Active</c> or <c>Suspended</c>).</summary>
    [HttpPatch("{identifier}/status")]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantDto>> SetStatus(
        [FromRoute] string identifier,
        [FromBody] SetTenantStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new SetTenantStatusCommand(identifier, request.Status),
            cancellationToken);

        return Ok(result);
    }
}

public sealed record CreateTenantRequest(string Identifier, string Name);

public sealed record SetTenantStatusRequest(string Status);
