using MediatR;
using PharmaPOS.Application.Common.Customers;

namespace PharmaPOS.Application.Features.Customers.ListCustomers;

public sealed class ListCustomersQueryHandler : IRequestHandler<ListCustomersQuery, IReadOnlyList<CustomerDto>>
{
    private readonly ICustomerRepository _customers;

    public ListCustomersQueryHandler(ICustomerRepository customers) => _customers = customers;

    public Task<IReadOnlyList<CustomerDto>> Handle(
        ListCustomersQuery request,
        CancellationToken cancellationToken)
        => _customers.ListAsync(
            request.Search, request.IsActive, request.Skip, request.Take, cancellationToken);
}
