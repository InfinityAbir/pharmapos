using MediatR;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Sales;

namespace PharmaPOS.Application.Features.Sales.Checkout;

public sealed class CheckoutCommandHandler : IRequestHandler<CheckoutCommand, CheckoutResult>
{
    private readonly ICheckoutService _checkout;
    private readonly IAuditLogger _auditLogger;

    public CheckoutCommandHandler(ICheckoutService checkout, IAuditLogger auditLogger)
    {
        _checkout = checkout;
        _auditLogger = auditLogger;
    }

    public async Task<CheckoutResult> Handle(
        CheckoutCommand request,
        CancellationToken cancellationToken)
    {
        var result = await _checkout.CheckoutAsync(request.Input, cancellationToken);

        // Replays return the stored outcome: auditing again would misrepresent history.
        if (!result.Replayed)
        {
            await _auditLogger.LogAsync(
                new AuditEntry(
                    Action: "sale.completed",
                    EntityType: "Sale",
                    EntityId: result.Sale.Id.ToString(),
                    Data: new Dictionary<string, string?>
                    {
                        ["invoice"] = result.Sale.InvoiceNumber,
                        ["total"] = result.Sale.TotalAmount.ToString("F4"),
                        ["lines"] = result.Sale.Items.Count.ToString(),
                        ["methods"] = string.Join(",", result.Sale.Payments
                            .Select(payment => payment.Method)
                            .Distinct()),
                    }),
                cancellationToken);
        }

        return result;
    }
}