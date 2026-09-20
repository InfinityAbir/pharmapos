using MediatR;
using PharmaPOS.Application.Features.Tenancy;

namespace PharmaPOS.Application.Features.Tenancy.SetTenantStatus;

public sealed record SetTenantStatusCommand(string Identifier, string Status) : IRequest<TenantDto>;