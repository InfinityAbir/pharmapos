using MediatR;
using Microsoft.AspNetCore.Mvc;
using PharmaPOS.Api.Authentication;
using PharmaPOS.Application.Common.Authorization;
using PharmaPOS.Application.Features.Tenancy;
using PharmaPOS.Application.Features.Tenancy.CreateRole;
using PharmaPOS.Application.Features.Tenancy.DeleteRole;
using PharmaPOS.Application.Features.Tenancy.GetMembers;
using PharmaPOS.Application.Features.Tenancy.GetRoles;
using PharmaPOS.Application.Features.Tenancy.ReplaceMembershipRoles;
using PharmaPOS.Application.Features.Tenancy.SetMembershipStatus;
using PharmaPOS.Application.Features.Tenancy.UpdateRole;

namespace PharmaPOS.Api.Controllers;

/// <summary>
/// Current-tenant administration: roles and memberships. Everything here is scoped to the active
/// tenant by the tenant-filtered context; cross-tenant references are rejected.
/// Controllers translate HTTP only; all behaviour lives in the application layer.
/// </summary>
[ApiController]
[Route("api/v1/tenant")]
public sealed class TenancyController : ControllerBase
{
    private readonly ISender _sender;

    public TenancyController(ISender sender) => _sender = sender;

    /// <summary>Lists the current tenant's roles with their granted permissions.</summary>
    [HasPermission(Permissions.Tenant.RolesManage)]
    [HttpGet("roles")]
    [ProducesResponseType(typeof(IReadOnlyList<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> GetRoles(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetRolesQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Creates a tenant role. Permission codes are validated against the platform catalog.</summary>
    [HasPermission(Permissions.Tenant.RolesManage)]
    [HttpPost("roles")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<RoleDto>> CreateRole(
        [FromBody] RoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreateRoleCommand(request.Name, request.Description, request.Permissions),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Updates a tenant role. System roles cannot be renamed.</summary>
    [HasPermission(Permissions.Tenant.RolesManage)]
    [HttpPut("roles/{id:guid}")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RoleDto>> UpdateRole(
        [FromRoute] Guid id,
        [FromBody] RoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateRoleCommand(id, request.Name, request.Description, request.Permissions),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Deletes a tenant role. System roles cannot be deleted.</summary>
    [HasPermission(Permissions.Tenant.RolesManage)]
    [HttpDelete("roles/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteRole(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteRoleCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>Lists the current tenant's members with their roles.</summary>
    [HasPermission(Permissions.Tenant.MembersManage)]
    [HttpGet("members")]
    [ProducesResponseType(typeof(IReadOnlyList<MemberDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MemberDto>>> GetMembers(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMembersQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Activates or deactivates a membership (<c>Active</c> / <c>Inactive</c>).</summary>
    [HasPermission(Permissions.Tenant.MembersManage)]
    [HttpPatch("members/{membershipId:guid}/status")]
    [ProducesResponseType(typeof(MemberDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MemberDto>> SetMembershipStatus(
        [FromRoute] Guid membershipId,
        [FromBody] SetMembershipStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new SetMembershipStatusCommand(membershipId, request.Status),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Replaces a membership's roles. Every role must belong to the current tenant.</summary>
    [HasPermission(Permissions.Tenant.MembersManage)]
    [HttpPut("members/{membershipId:guid}/roles")]
    [ProducesResponseType(typeof(MemberDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MemberDto>> ReplaceMembershipRoles(
        [FromRoute] Guid membershipId,
        [FromBody] ReplaceMembershipRolesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ReplaceMembershipRolesCommand(membershipId, request.RoleIds),
            cancellationToken);

        return Ok(result);
    }
}

public sealed record RoleRequest(
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions);

public sealed record SetMembershipStatusRequest(string Status);

public sealed record ReplaceMembershipRolesRequest(IReadOnlyList<Guid> RoleIds);
