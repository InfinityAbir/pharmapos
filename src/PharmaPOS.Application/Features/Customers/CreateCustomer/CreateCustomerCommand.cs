using MediatR;
using PharmaPOS.Application.Common.Customers;

namespace PharmaPOS.Application.Features.Customers.CreateCustomer;

public sealed record CreateCustomerCommand(CustomerInput Input) : IRequest<CustomerDto>;
