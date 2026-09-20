using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaPOS.Api.Authentication;
using PharmaPOS.Application.Common.Authorization;
using PharmaPOS.Application.Features.Auditing;
using PharmaPOS.Application.Features.Auditing.GetAuditLogs;
using PharmaPOS.Application.Features.Identity.GetCurrentMembership;
using PharmaPOS.Application.Features.Platform.GetPlatformInfo;
using PharmaPOS.Application.Features.Tenancy;
using PharmaPOS.Domain.Exceptions;

namespace PharmaPOS.Api.Controllers;

/// <summary>
/// Thin endpoint surface for platform-level information, identity introspection and audit.
/// Controllers translate HTTP only; all behaviour lives in the application layer.
/// </summary>
[ApiController]
[Route("api/v1/platform")]
public sealed class PlatformController : ControllerBase
{
    private readonly ISender _sender;

    public PlatformController(ISender sender) => _sender = sender;

    /// <summary>Returns basic API information. Anonymous; never contains secrets or tenant data.</summary>
    [AllowAnonymous]
    [HttpGet("info")]
    [ProducesResponseType(typeof(PlatformInfoDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PlatformInfoDto>> GetInfo(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetPlatformInfoQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns the caller's server-resolved session context: identity, active tenant, membership
    /// status, roles and permissions from the database membership graph. Callers without an active
    /// membership in the tenant receive 403; it never echoes client-supplied values.
    /// </summary>
    [Authorize]
    [HttpGet("whoami")]
    [ProducesResponseType(typeof(CurrentMembershipDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentMembershipDto>> WhoAmI(CancellationToken cancellationToken)
    {
        var tenantIdentifier = User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrWhiteSpace(tenantIdentifier))
        {
            throw new ForbiddenException(
                "tenant.unresolved",
                "The access token carries no tenant context.");
        }

        var result = await _sender.Send(
            new GetCurrentMembershipQuery(
                Subject: User.FindFirst("sub")?.Value ?? string.Empty,
                Username: User.FindFirst("preferred_username")?.Value ?? string.Empty,
                TenantIdentifier: tenantIdentifier),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Returns recent audit entries for the current tenant. Requires the audit-read permission.</summary>
    [HasPermission(Permissions.Platform.AuditRead)]
    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AuditLogDto>>> GetAuditLogs(
        CancellationToken cancellationToken,
        [FromQuery] int take = 50)
    {
        var result = await _sender.Send(new GetAuditLogsQuery(take), cancellationToken);
        return Ok(result);
    }
}
