using MediatR;

namespace PharmaPOS.Application.Features.Customers.DeleteCustomer;

public sealed record DeleteCustomerCommand(Guid Id) : IRequest;
