using MediatR;
using PharmaPOS.Application.Common.Tenancy;
using PharmaPOS.Application.Features.Tenancy;

namespace PharmaPOS.Application.Features.Tenancy.ListTenants;

public sealed class ListTenantsQueryHandler : IRequestHandler<ListTenantsQuery, IReadOnlyList<TenantDto>>
{
    private readonly IPlatformTenantStore _tenantStore;

    public ListTenantsQueryHandler(IPlatformTenantStore tenantStore) => _tenantStore = tenantStore;

    public Task<IReadOnlyList<TenantDto>> Handle(
        ListTenantsQuery request,
        CancellationToken cancellationToken)
        => _tenantStore.ListAsync(cancellationToken);
}