using MediatR;
using PharmaPOS.Application.Common.Tenancy;
using PharmaPOS.Application.Features.Tenancy;

namespace PharmaPOS.Application.Features.Tenancy.GetRoles;

public sealed class GetRolesQueryHandler : IRequestHandler<GetRolesQuery, IReadOnlyList<RoleDto>>
{
    private readonly ITenantRoleRepository _roleRepository;

    public GetRolesQueryHandler(ITenantRoleRepository roleRepository) => _roleRepository = roleRepository;

    public Task<IReadOnlyList<RoleDto>> Handle(
        GetRolesQuery request,
        CancellationToken cancellationToken)
        => _roleRepository.ListAsync(cancellationToken);
}