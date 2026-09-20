using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Customers;

namespace PharmaPOS.Application.Features.Customers.UpdateCustomer;

public sealed class UpdateCustomerCommandHandler : IRequestHandler<UpdateCustomerCommand, CustomerDto>
{
    private readonly ICustomerRepository _customers;
    private readonly IAuditLogger _auditLogger;

    public UpdateCustomerCommandHandler(ICustomerRepository customers, IAuditLogger auditLogger)
    {
        _customers = customers;
        _auditLogger = auditLogger;
    }

    public async Task<CustomerDto> Handle(
        UpdateCustomerCommand request,
        CancellationToken cancellationToken)
    {
        var customer = await _customers.UpdateAsync(request.Id, request.Input, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "customer.updated",
                EntityType: "Customer",
                EntityId: customer.Id.ToString(),
                Data: new Dictionary<string, string?> { ["name"] = customer.Name }),
            cancellationToken);

        return customer;
    }
}
