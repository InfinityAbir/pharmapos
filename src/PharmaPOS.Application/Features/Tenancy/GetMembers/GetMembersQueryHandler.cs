using MediatR;
using PharmaPOS.Application.Common.Tenancy;
using PharmaPOS.Application.Features.Tenancy;

namespace PharmaPOS.Application.Features.Tenancy.GetMembers;

public sealed class GetMembersQueryHandler : IRequestHandler<GetMembersQuery, IReadOnlyList<MemberDto>>
{
    private readonly ITenantMembershipRepository _membershipRepository;

    public GetMembersQueryHandler(ITenantMembershipRepository membershipRepository)
        => _membershipRepository = membershipRepository;

    public Task<IReadOnlyList<MemberDto>> Handle(
        GetMembersQuery request,
        CancellationToken cancellationToken)
        => _membershipRepository.ListAsync(cancellationToken);
}