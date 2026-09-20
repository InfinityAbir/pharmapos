using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Tenancy;
using PharmaPOS.Application.Features.Tenancy;

namespace PharmaPOS.Application.Features.Tenancy.SetMembershipStatus;

public sealed class SetMembershipStatusCommandHandler : IRequestHandler<SetMembershipStatusCommand, MemberDto>
{
    private readonly ITenantMembershipRepository _membershipRepository;
    private readonly IAuditLogger _auditLogger;

    public SetMembershipStatusCommandHandler(
        ITenantMembershipRepository membershipRepository,
        IAuditLogger auditLogger)
    {
        _membershipRepository = membershipRepository;
        _auditLogger = auditLogger;
    }

    public async Task<MemberDto> Handle(SetMembershipStatusCommand request, CancellationToken cancellationToken)
    {
        var member = await _membershipRepository.SetStatusAsync(
            request.MembershipId,
            request.Status,
            cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "membership.status.changed",
                EntityType: "TenantMembership",
                EntityId: member.MembershipId.ToString(),
                Data: new Dictionary<string, string?> { ["status"] = member.Status }),
            cancellationToken);

        return member;
    }
}