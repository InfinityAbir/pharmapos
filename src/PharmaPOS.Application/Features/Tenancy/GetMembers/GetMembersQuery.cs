using MediatR;
using PharmaPOS.Application.Features.Tenancy;

namespace PharmaPOS.Application.Features.Tenancy.GetMembers;

public sealed record GetMembersQuery : IRequest<IReadOnlyList<MemberDto>>;