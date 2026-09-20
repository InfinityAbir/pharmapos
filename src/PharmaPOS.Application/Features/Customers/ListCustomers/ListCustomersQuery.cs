using MediatR;

namespace PharmaPOS.Application.Features.Customers.ListCustomers;

public sealed record ListCustomersQuery(
    string? Search,
    bool? IsActive,
    int Skip,
    int Take) : IRequest<IReadOnlyList<CustomerDto>>;
