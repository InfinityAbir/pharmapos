using MediatR;
using PharmaPOS.Application.Common.Purchasing;

namespace PharmaPOS.Application.Features.Purchasing.ReceivePurchase;

public sealed record ReceivePurchaseCommand(Guid Id, string IdempotencyKey)
    : IRequest<ReceivePurchaseResult>;