using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Customers;

namespace PharmaPOS.Application.Features.Customers.DeleteCustomer;

public sealed class DeleteCustomerCommandHandler : IRequestHandler<DeleteCustomerCommand>
{
    private readonly ICustomerRepository _customers;
    private readonly IAuditLogger _auditLogger;

    public DeleteCustomerCommandHandler(ICustomerRepository customers, IAuditLogger auditLogger)
    {
        _customers = customers;
        _auditLogger = auditLogger;
    }

    public async Task Handle(
        DeleteCustomerCommand request,
        CancellationToken cancellationToken)
    {
        await _customers.DeleteAsync(request.Id, cancellationToken);

        await _auditLogger.LogAsync(
            new AuditEntry(
                Action: "customer.deleted",
                EntityType: "Customer",
                EntityId: request.Id.ToString(),
                Data: null),
            cancellationToken);
    }
}
