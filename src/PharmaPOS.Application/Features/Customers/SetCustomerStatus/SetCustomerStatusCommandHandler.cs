using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Customers;

namespace PharmaPOS.Application.Features.Customers.SetCustomerStatus;

public sealed class SetCustomerStatusCommandHandler : IRequestHandler<SetCustomerStatusCommand, CustomerDto>
{
    private readonly ICustomerRepository _customers;
    private readonly IAuditLogger _auditLogger;

    public SetCustomerStatusCommandHandler(ICustomerRepository customers, IAuditLogger auditLogger)
    {
        _customers = customers;
        _auditLogger = auditLogger;
    }

    public async Task<CustomerDto> Handle(
        SetCustomerStatusCommand request,
        CancellationToken cancellationToken)
    {
        var customer = await _customers.SetActiveAsync(request.Id, request.IsActive, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: request.IsActive ? "customer.activated" : "customer.deactivated",
                EntityType: "Customer",
                EntityId: customer.Id.ToString(),
                Data: null),
            cancellationToken);

        return customer;
    }
}
