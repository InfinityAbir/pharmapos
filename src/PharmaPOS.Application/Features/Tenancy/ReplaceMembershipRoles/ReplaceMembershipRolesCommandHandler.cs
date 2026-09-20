using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Tenancy;
using PharmaPOS.Application.Features.Tenancy;

namespace PharmaPOS.Application.Features.Tenancy.ReplaceMembershipRoles;

public sealed class ReplaceMembershipRolesCommandHandler
    : IRequestHandler<ReplaceMembershipRolesCommand, MemberDto>
{
    private readonly ITenantMembershipRepository _membershipRepository;
    private readonly IAuditLogger _auditLogger;

    public ReplaceMembershipRolesCommandHandler(
        ITenantMembershipRepository membershipRepository,
        IAuditLogger auditLogger)
    {
        _membershipRepository = membershipRepository;
        _auditLogger = auditLogger;
    }

    public async Task<MemberDto> Handle(
        ReplaceMembershipRolesCommand request,
        CancellationToken cancellationToken)
    {
        var member = await _membershipRepository.ReplaceRolesAsync(
            request.MembershipId,
            request.RoleIds,
            cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "membership.roles.updated",
                EntityType: "TenantMembership",
                EntityId: member.MembershipId.ToString(),
                Data: new Dictionary<string, string?>
                {
                    ["roles"] = string.Join(",", member.Roles),
                }),
            cancellationToken);

        return member;
    }
}