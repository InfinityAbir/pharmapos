using MediatR;
using PharmaPOS.Application.Features.Tenancy;

namespace PharmaPOS.Application.Features.Tenancy.SetMembershipStatus;

public sealed record SetMembershipStatusCommand(Guid MembershipId, string Status) : IRequest<MemberDto>;