using FluentAssertions;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Purchasing;
using PharmaPOS.Application.Features.Purchasing;
using PharmaPOS.Application.Features.Purchasing.ReceivePurchase;
using Xunit;

namespace PharmaPOS.UnitTests;

public sealed class ReceivePurchaseHandlerTests
{
    [Fact]
    public async Task Audits_only_on_first_receive_not_on_replay()
    {
        var purchase = TestPurchase();
        var audit = new CapturingAuditLogger();

        var first = await new ReceivePurchaseCommandHandler(
                new StubPurchases(new ReceivePurchaseResult(purchase, Replayed: false)), audit)
            .Handle(new ReceivePurchaseCommand(purchase.Id, "key-1"), CancellationToken.None);

        first.Replayed.Should().BeFalse();
        audit.Entries.Should().ContainSingle()
            .Which.Action.Should().Be("purchase.received");

        var replay = await new ReceivePurchaseCommandHandler(
                new StubPurchases(new ReceivePurchaseResult(purchase, Replayed: true)), audit)
            .Handle(new ReceivePurchaseCommand(purchase.Id, "key-1"), CancellationToken.None);

        replay.Replayed.Should().BeTrue();
        audit.Entries.Should().ContainSingle();
    }

    private static PurchaseDto TestPurchase() => new(
        Id: Guid.NewGuid(),
        SupplierId: Guid.NewGuid(),
        SupplierName: "Square Distributor",
        PurchaseDate: DateOnly.FromDateTime(DateTime.UtcNow.Date),
        Status: "Received",
        SupplierInvoiceNumber: null,
        Subtotal: 80.00m,
        DiscountAmount: 0,
        TaxAmount: 0,
        TotalAmount: 80.00m,
        Notes: null,
        CreatedAtUtc: DateTimeOffset.UtcNow,
        ReceivedAtUtc: DateTimeOffset.UtcNow,
        Items: []);

    private sealed class StubPurchases(ReceivePurchaseResult result) : IPurchaseRepository
    {
        public Task<ReceivePurchaseResult> ReceiveAsync(
            Guid id,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result);

        public Task<IReadOnlyList<PurchaseDto>> ListAsync(
            Guid? supplierId, string? status, int skip, int take,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PurchaseDto?> FindAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PurchaseDto> CreateDraftAsync(
            PurchaseInput input, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PurchaseDto> UpdateDraftAsync(
            Guid id, PurchaseInput input, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PurchaseDto> CancelAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PurchasingSummaryDto> GetSummaryAsync(
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
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
