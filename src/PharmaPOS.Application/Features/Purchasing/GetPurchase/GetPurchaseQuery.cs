using MediatR;

namespace PharmaPOS.Application.Features.Purchasing.GetPurchase;

public sealed record GetPurchaseQuery(Guid Id) : IRequest<PurchaseDto>;