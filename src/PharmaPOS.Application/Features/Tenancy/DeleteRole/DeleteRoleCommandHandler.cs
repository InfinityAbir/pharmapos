using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Tenancy;

namespace PharmaPOS.Application.Features.Tenancy.DeleteRole;

public sealed class DeleteRoleCommandHandler : IRequestHandler<DeleteRoleCommand, Unit>
{
    private readonly ITenantRoleRepository _roleRepository;
    private readonly IAuditLogger _auditLogger;

    public DeleteRoleCommandHandler(ITenantRoleRepository roleRepository, IAuditLogger auditLogger)
    {
        _roleRepository = roleRepository;
        _auditLogger = auditLogger;
    }

    public async Task<Unit> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        await _roleRepository.DeleteAsync(request.Id, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "role.deleted",
                EntityType: "Role",
                EntityId: request.Id.ToString()),
            cancellationToken);

        return Unit.Value;
    }
}