using MediatR;
using PharmaPOS.Application.Features.Tenancy;

namespace PharmaPOS.Application.Features.Tenancy.ReplaceMembershipRoles;

public sealed record ReplaceMembershipRolesCommand(
    Guid MembershipId,
    IReadOnlyList<Guid> RoleIds) : IRequest<MemberDto>;