using MediatR;

namespace PharmaPOS.Application.Features.Purchasing.GetSupplier;

public sealed record GetSupplierQuery(Guid Id) : IRequest<SupplierDto>;