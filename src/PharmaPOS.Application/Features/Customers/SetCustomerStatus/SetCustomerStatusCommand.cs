using MediatR;

namespace PharmaPOS.Application.Features.Customers.SetCustomerStatus;

public sealed record SetCustomerStatusCommand(Guid Id, bool IsActive) : IRequest<CustomerDto>;
