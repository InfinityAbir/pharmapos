using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Sales;

namespace PharmaPOS.Application.Features.Returns.ProcessReturn;

public sealed class ProcessReturnCommandHandler : IRequestHandler<ProcessReturnCommand, ProcessReturnResult>
{
    private readonly IReturnService _returns;
    private readonly IAuditLogger _auditLogger;

    public ProcessReturnCommandHandler(IReturnService returns, IAuditLogger auditLogger)
    {
        _returns = returns;
        _auditLogger = auditLogger;
    }

    public async Task<ProcessReturnResult> Handle(
        ProcessReturnCommand request,
        CancellationToken cancellationToken)
    {
        var result = await _returns.ProcessAsync(request.Input, cancellationToken);

        if (!result.Replayed)
        {
            await _auditLogger.LogAsync(
                new AuditEntry(
                    Action: "sale.returned",
                    EntityType: "SaleReturn",
                    EntityId: result.Return.Id.ToString(),
                    Data: new Dictionary<string, string?>
                    {
                        ["saleId"] = result.Return.SaleId.ToString(),
                        ["invoice"] = result.Return.InvoiceNumber,
                        ["refund"] = result.Return.RefundAmount.ToString("F4"),
                        ["method"] = result.Return.RefundMethod,
                        ["restock"] = result.Return.Restock.ToString(),
                    }),
                cancellationToken);
        }

        return result;
    }
}
