using MediatR;

namespace PharmaPOS.Application.Features.Customers.GetCustomer;

public sealed record GetCustomerQuery(Guid Id) : IRequest<CustomerWithHistoryDto>;
