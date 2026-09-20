using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Tenancy;
using PharmaPOS.Application.Features.Tenancy;

namespace PharmaPOS.Application.Features.Tenancy.CreateRole;

public sealed class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, RoleDto>
{
    private readonly ITenantRoleRepository _roleRepository;
    private readonly IAuditLogger _auditLogger;

    public CreateRoleCommandHandler(ITenantRoleRepository roleRepository, IAuditLogger auditLogger)
    {
        _roleRepository = roleRepository;
        _auditLogger = auditLogger;
    }

    public async Task<RoleDto> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.CreateAsync(
            request.Name,
            request.Description,
            request.Permissions,
            cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "role.created",
                EntityType: "Role",
                EntityId: role.Id.ToString(),
                Data: new Dictionary<string, string?> { ["name"] = role.Name }),
            cancellationToken);

        return role;
    }
}