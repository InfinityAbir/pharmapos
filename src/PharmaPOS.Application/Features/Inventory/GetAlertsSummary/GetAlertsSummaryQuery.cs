using MediatR;

namespace PharmaPOS.Application.Features.Inventory.GetAlertsSummary;

public sealed record GetAlertsSummaryQuery : IRequest<AlertsSummaryDto>;