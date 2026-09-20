using MediatR;

namespace PharmaPOS.Application.Features.Sales.GetTodaySales;

public sealed record GetTodaySalesQuery : IRequest<TodaySalesDto>;