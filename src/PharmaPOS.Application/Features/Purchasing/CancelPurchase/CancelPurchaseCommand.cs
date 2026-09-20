using MediatR;

namespace PharmaPOS.Application.Features.Purchasing.CancelPurchase;

public sealed record CancelPurchaseCommand(Guid Id) : IRequest<PurchaseDto>;