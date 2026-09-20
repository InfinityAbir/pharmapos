using MediatR;

namespace PharmaPOS.Application.Features.Sales.GetSale;

public sealed record GetSaleQuery(Guid Id) : IRequest<SaleDto>;