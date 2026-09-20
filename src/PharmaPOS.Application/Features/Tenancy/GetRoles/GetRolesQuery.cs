using MediatR;
using PharmaPOS.Application.Features.Tenancy;

namespace PharmaPOS.Application.Features.Tenancy.GetRoles;

public sealed record GetRolesQuery : IRequest<IReadOnlyList<RoleDto>>;