using FluentAssertions;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Sales;
using PharmaPOS.Application.Features.Sales;
using PharmaPOS.Application.Features.Sales.Checkout;
using Xunit;

namespace PharmaPOS.UnitTests;

public sealed class CheckoutHandlerTests
{
    [Fact]
    public async Task Audits_only_on_first_checkout_not_on_replay()
    {
        var sale = TestSale();
        var audit = new CapturingAuditLogger();

        var first = await new CheckoutCommandHandler(
                new StubCheckout(new CheckoutResult(sale, Replayed: false)), audit)
            .Handle(new CheckoutCommand(TestInput()), CancellationToken.None);

        first.Replayed.Should().BeFalse();
        audit.Entries.Should().ContainSingle()
            .Which.Action.Should().Be("sale.completed");

        var replay = await new CheckoutCommandHandler(
                new StubCheckout(new CheckoutResult(sale, Replayed: true)), audit)
            .Handle(new CheckoutCommand(TestInput()), CancellationToken.None);

        replay.Replayed.Should().BeTrue();
        audit.Entries.Should().ContainSingle();
    }

    private static CheckoutInput TestInput() => new(
        Items: [new CheckoutItemInput(Guid.NewGuid(), 1, null, 0)],
        Payments: [new CheckoutPaymentInput("Cash", 10.00m, null)],
        DiscountAmount: 0,
        CustomerId: null,
        CustomerName: null,
        CustomerPhone: null,
        PrescriptionReference: null,
        IdempotencyKey: "key-1");

    private static SaleDto TestSale() => new(
        Id: Guid.NewGuid(),
        InvoiceNumber: "INV-000001",
        OccurredAtUtc: DateTimeOffset.UtcNow,
        Subtotal: 10.00m,
        DiscountAmount: 0,
        TotalAmount: 10.00m,
        PaidAmount: 10.00m,
        ChangeDue: 0,
        CustomerId: null,
        CustomerName: null,
        CustomerPhone: null,
        PrescriptionReference: null,
        CashierSubject: "admin",
        Items: [],
        Payments: []);

    private sealed class StubCheckout(CheckoutResult result) : ICheckoutService
    {
        public Task<CheckoutResult> CheckoutAsync(
            CheckoutInput input,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }

    private sealed class CapturingAuditLogger : IAuditLogger
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}
