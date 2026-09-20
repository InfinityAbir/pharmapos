using MediatR;

namespace PharmaPOS.Application.Features.Tenancy.DeleteRole;

public sealed record DeleteRoleCommand(Guid Id) : IRequest<Unit>;