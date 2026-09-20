using MediatR;
using PharmaPOS.Application.Features.Tenancy;

namespace PharmaPOS.Application.Features.Tenancy.CreateTenant;

public sealed record CreateTenantCommand(string Identifier, string Name) : IRequest<TenantDto>;