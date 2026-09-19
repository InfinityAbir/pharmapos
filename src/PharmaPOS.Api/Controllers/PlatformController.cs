using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaPOS.Api.Authentication;
using PharmaPOS.Application.Common.Authorization;
using PharmaPOS.Application.Features.Auditing;
using PharmaPOS.Application.Features.Auditing.GetAuditLogs;
using PharmaPOS.Application.Features.Platform.GetPlatformInfo;

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
    /// Returns the authenticated caller's server-validated identity and tenant context. Useful for
    /// the frontend shell to confirm the session; it never echoes client-supplied values.
    /// </summary>
    [Authorize]
    [HttpGet("whoami")]
    public ActionResult<WhoAmIResponse> WhoAmI()
    {
        var permissions = User
            .FindAll(PermissionAuthorizationHandler.PermissionClaimType)
            .Select(claim => claim.Value)
            .Distinct()
            .ToArray();

        return Ok(new WhoAmIResponse(
            Subject: User.FindFirst("sub")?.Value,
            Username: User.FindFirst("preferred_username")?.Value,
            Email: User.FindFirst("email")?.Value,
            TenantIdentifier: User.FindFirst("tenant_id")?.Value,
            Permissions: permissions));
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

public sealed record WhoAmIResponse(
    string? Subject,
    string? Username,
    string? Email,
    string? TenantIdentifier,
    IReadOnlyList<string> Permissions);
