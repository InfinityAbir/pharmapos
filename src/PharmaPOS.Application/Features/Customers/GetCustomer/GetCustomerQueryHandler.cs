using MediatR;
using PharmaPOS.Application.Common.Customers;
using PharmaPOS.Domain.Exceptions;

namespace PharmaPOS.Application.Features.Customers.GetCustomer;

public sealed class GetCustomerQueryHandler : IRequestHandler<GetCustomerQuery, CustomerWithHistoryDto>
{
    private readonly ICustomerRepository _customers;

    public GetCustomerQueryHandler(ICustomerRepository customers) => _customers = customers;

    public async Task<CustomerWithHistoryDto> Handle(
        GetCustomerQuery request,
        CancellationToken cancellationToken)
    {
        var result = await _customers.FindWithHistoryAsync(request.Id, cancellationToken);
        if (result is null)
        {
            throw new NotFoundException("customer.not_found", "Customer not found in this tenant.");
        }

        return result;
    }
}
