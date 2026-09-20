using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Tenancy;
using PharmaPOS.Application.Features.Tenancy;

namespace PharmaPOS.Application.Features.Tenancy.UpdateRole;

public sealed class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, RoleDto>
{
    private readonly ITenantRoleRepository _roleRepository;
    private readonly IAuditLogger _auditLogger;

    public UpdateRoleCommandHandler(ITenantRoleRepository roleRepository, IAuditLogger auditLogger)
    {
        _roleRepository = roleRepository;
        _auditLogger = auditLogger;
    }

    public async Task<RoleDto> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.UpdateAsync(
            request.Id,
            request.Name,
            request.Description,
            request.Permissions,
            cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "role.updated",
                EntityType: "Role",
                EntityId: role.Id.ToString(),
                Data: new Dictionary<string, string?> { ["name"] = role.Name }),
            cancellationToken);

        return role;
    }
}