using MediatR;
using PharmaPOS.Application.Features.Tenancy;

namespace PharmaPOS.Application.Features.Tenancy.ListTenants;

public sealed record ListTenantsQuery : IRequest<IReadOnlyList<TenantDto>>;