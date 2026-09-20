using MediatR;

namespace PharmaPOS.Application.Features.Purchasing.GetPurchasingSummary;

public sealed record GetPurchasingSummaryQuery : IRequest<PurchasingSummaryDto>;