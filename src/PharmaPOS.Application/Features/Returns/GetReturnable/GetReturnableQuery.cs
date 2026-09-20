using MediatR;

namespace PharmaPOS.Application.Features.Returns.GetReturnable;

public sealed record GetReturnableQuery(Guid SaleId) : IRequest<ReturnableSaleDto>;
