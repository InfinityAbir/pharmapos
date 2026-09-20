using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Customers;

namespace PharmaPOS.Application.Features.Customers.CreateCustomer;

public sealed class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, CustomerDto>
{
    private readonly ICustomerRepository _customers;
    private readonly IAuditLogger _auditLogger;

    public CreateCustomerCommandHandler(ICustomerRepository customers, IAuditLogger auditLogger)
    {
        _customers = customers;
        _auditLogger = auditLogger;
    }

    public async Task<CustomerDto> Handle(
        CreateCustomerCommand request,
        CancellationToken cancellationToken)
    {
        var customer = await _customers.CreateAsync(request.Input, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "customer.created",
                EntityType: "Customer",
                EntityId: customer.Id.ToString(),
                Data: new Dictionary<string, string?> { ["name"] = customer.Name }),
            cancellationToken);

        return customer;
    }
}
