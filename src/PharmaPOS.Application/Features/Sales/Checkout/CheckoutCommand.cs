using MediatR;
using PharmaPOS.Application.Common.Sales;

namespace PharmaPOS.Application.Features.Sales.Checkout;

public sealed record CheckoutCommand(CheckoutInput Input) : IRequest<CheckoutResult>;