using MediatR;
using PharmaPOS.Application.Common.Customers;

namespace PharmaPOS.Application.Features.Customers.UpdateCustomer;

public sealed record UpdateCustomerCommand(Guid Id, CustomerInput Input) : IRequest<CustomerDto>;
