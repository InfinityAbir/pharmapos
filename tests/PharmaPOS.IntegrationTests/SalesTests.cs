using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PharmaPOS.Application.Common.Sales;
using PharmaPOS.Domain.Exceptions;
using PharmaPOS.Infrastructure.Persistence;
using PharmaPOS.Infrastructure.Sales;
using Xunit;

namespace PharmaPOS.IntegrationTests;

/// <summary>
/// Proves the Phase 5 checkout model against PostgreSQL: atomic completion with server-side
/// pricing, FEFO allocation skipping expired stock, change math, idempotent replay, transition
/// guards, concurrency mapping to 409, and cross-tenant invisibility.
/// </summary>
public sealed class SalesTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string TenantA = "tenant-alpha";
    private const string TenantB = "tenant-beta";

    private readonly PostgresFixture _fixture;

    public SalesTests(PostgresFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Checkout_posts_sale_batches_and_movements_atomically()
    {
        Guid medicineId = await SeedMedicineAsync(TenantA, "Napa", price: 12.00m);
        await SeedBatchAsync(TenantA, medicineId, "NP-001", quantity: 100, daysToExpiry: 400);

        await using var db = _fixture.CreateAppContext(TenantA);
        var result = await new CheckoutService(db, new NullAccessor()).CheckoutAsync(
            Input(
                items: [new CheckoutItemInput(medicineId, 3, null, 2.00m)],
                payments: [new CheckoutPaymentInput("Cash", 40.00m, null)]),
            CancellationToken.None);

        result.Replayed.Should().BeFalse();

        // Note: the seeded batch carries price 10 (not the catalog 12) — the server must
        // use the batch price: 3 × 10 = 30 gross, −2 discount = 28 total, 40 − 28 = 12 change.
        var sale = result.Sale;
        sale.InvoiceNumber.Should().Be("INV-000001");
        sale.Subtotal.Should().Be(30.00m);
        sale.DiscountAmount.Should().Be(2.00m);
        sale.TotalAmount.Should().Be(28.00m);
        sale.PaidAmount.Should().Be(40.00m);
        sale.ChangeDue.Should().Be(12.00m);
        sale.Items.Should().ContainSingle()
            .Which.Should().Match<PharmaPOS.Application.Features.Sales.SaleItemDto>(item =>
                item.Quantity == 3
                && item.UnitPrice == 10.00m
                && item.DiscountAmount == 2.00m
                && item.LineTotal == 28.00m);

        (await db.MedicineBatches.SingleAsync()).Quantity.Should().Be(97);

        var movements = await db.InventoryMovements.ToListAsync();
        movements.Should().ContainSingle()
            .Which.Should().Match<PharmaPOS.Domain.Inventory.InventoryMovement>(movement =>
                movement.Type == "SALE"
                && movement.QuantityChange == -3
                && movement.QuantityAfter == 97);
    }

    [Fact]
    public async Task Checkout_allocates_fefo_and_skips_expired_batches()
    {
        Guid medicineId = await SeedMedicineAsync(TenantA, "Napa", price: 10.00m);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        await using (var setup = _fixture.CreateAppContext(TenantA))
        {
            await AddBatchAsync(setup, medicineId, "LATER", quantity: 100,
                expiry: today.AddDays(300), price: 10.00m);
            await AddBatchAsync(setup, medicineId, "SOON", quantity: 5,
                expiry: today.AddDays(10), price: 10.00m);
            await AddBatchAsync(setup, medicineId, "OLD", quantity: 50,
                expiry: today.AddDays(-5), price: 10.00m);
        }

        await using var db = _fixture.CreateAppContext(TenantA);
        var result = await new CheckoutService(db, new NullAccessor()).CheckoutAsync(
            Input(
                items: [new CheckoutItemInput(medicineId, 8, null, 0)],
                payments: [new CheckoutPaymentInput("Cash", 80.00m, null)]),
            CancellationToken.None);

        // SOON (5) drains first, then LATER (3); expired OLD untouched.
        var quantities = await db.MedicineBatches
            .OrderBy(batch => batch.BatchNumber)
            .Select(batch => batch.Quantity)
            .ToListAsync();

        quantities.Should().BeEquivalentTo([97, 50, 0]);
        result.Sale.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Checkout_refuses_shortfalls_inactive_expired_only_and_underpayment()
    {
        Guid medicineId = await SeedMedicineAsync(TenantA, "Napa", price: 10.00m);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        await using (var setup = _fixture.CreateAppContext(TenantA))
        {
            await AddBatchAsync(setup, medicineId, "NP-001", quantity: 5,
                expiry: today.AddDays(100), price: 10.00m);
            await AddBatchAsync(setup, medicineId, "OLD", quantity: 50,
                expiry: today.AddDays(-5), price: 10.00m);
        }

        await using var db = _fixture.CreateAppContext(TenantA);
        var checkout = new CheckoutService(db, new NullAccessor());

        var shortfall = async () => await checkout.CheckoutAsync(
            Input(
                items: [new CheckoutItemInput(medicineId, 6, null, 0)],
                payments: [new CheckoutPaymentInput("Cash", 60.00m, null)]),
            CancellationToken.None);
        (await shortfall.Should().ThrowAsync<BusinessRuleException>())
            .Which.Code.Should().Be("sale.insufficient_stock");

        var underpaid = async () => await checkout.CheckoutAsync(
            Input(
                items: [new CheckoutItemInput(medicineId, 2, null, 0)],
                payments: [new CheckoutPaymentInput("Cash", 19.99m, null)]),
            CancellationToken.None);
        (await underpaid.Should().ThrowAsync<BusinessRuleException>())
            .Which.Code.Should().Be("sale.underpaid");

        var overDiscount = async () => await checkout.CheckoutAsync(
            Input(
                items: [new CheckoutItemInput(medicineId, 1, null, 11.00m)],
                payments: [new CheckoutPaymentInput("Cash", 10.00m, null)]),
            CancellationToken.None);
        (await overDiscount.Should().ThrowAsync<BusinessRuleException>())
            .Which.Code.Should().Be("sale.discount_exceeded");

        // Nothing moved: the failed attempts posted no stock changes.
        (await db.MedicineBatches.SumAsync(batch => batch.Quantity)).Should().Be(55);
        (await db.InventoryMovements.CountAsync()).Should().Be(0);
        (await db.Sales.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Checkout_honours_an_explicit_batch_override()
    {
        Guid medicineId = await SeedMedicineAsync(TenantA, "Napa", price: 10.00m);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        Guid laterId;
        await using (var setup = _fixture.CreateAppContext(TenantA))
        {
            await AddBatchAsync(setup, medicineId, "SOON", quantity: 100,
                expiry: today.AddDays(10), price: 10.00m);
            laterId = await AddBatchAsync(setup, medicineId, "LATER", quantity: 100,
                expiry: today.AddDays(300), price: 10.00m);
        }

        await using var db = _fixture.CreateAppContext(TenantA);
        var result = await new CheckoutService(db, new NullAccessor()).CheckoutAsync(
            Input(
                items: [new CheckoutItemInput(medicineId, 4, laterId, 0)],
                payments: [new CheckoutPaymentInput("Cash", 40.00m, null)]),
            CancellationToken.None);

        result.Sale.Items.Should().ContainSingle()
            .Which.BatchId.Should().Be(laterId);

        var soon = await db.MedicineBatches.SingleAsync(batch => batch.BatchNumber == "SOON");
        soon.Quantity.Should().Be(100);
    }

    [Fact]
    public async Task Replay_returns_the_stored_sale_without_side_effects()
    {
        Guid medicineId = await SeedMedicineAsync(TenantA, "Napa", price: 12.00m);
        await SeedBatchAsync(TenantA, medicineId, "NP-001", quantity: 100, daysToExpiry: 400);

        await using var db = _fixture.CreateAppContext(TenantA);
        var checkout = new CheckoutService(db, new NullAccessor());
        var input = Input(
            items: [new CheckoutItemInput(medicineId, 3, null, 0)],
            payments: [new CheckoutPaymentInput("Cash", 36.00m, null)],
            key: "sale-key-1");

        var first = await checkout.CheckoutAsync(input, CancellationToken.None);
        first.Replayed.Should().BeFalse();

        var replay = await checkout.CheckoutAsync(input, CancellationToken.None);
        replay.Replayed.Should().BeTrue();
        replay.Sale.Id.Should().Be(first.Sale.Id);
        replay.Sale.InvoiceNumber.Should().Be("INV-000001");

        (await db.MedicineBatches.SingleAsync()).Quantity.Should().Be(97);
        (await db.InventoryMovements.CountAsync()).Should().Be(1);
        (await db.Sales.CountAsync()).Should().Be(1);

        // Sequential invoice numbers keep increasing across sales.
        var second = await checkout.CheckoutAsync(
            Input(
                items: [new CheckoutItemInput(medicineId, 1, null, 0)],
                payments: [new CheckoutPaymentInput("Cash", 12.00m, null)],
                key: "sale-key-2"),
            CancellationToken.None);
        second.Sale.InvoiceNumber.Should().Be("INV-000002");
    }

    [Fact]
    public async Task Split_tender_with_reference_records_all_payments()
    {
        Guid medicineId = await SeedMedicineAsync(TenantA, "Napa", price: 10.00m);
        await SeedBatchAsync(TenantA, medicineId, "NP-001", quantity: 100, daysToExpiry: 400);

        await using var db = _fixture.CreateAppContext(TenantA);
        var result = await new CheckoutService(db, new NullAccessor()).CheckoutAsync(
            Input(
                items: [new CheckoutItemInput(medicineId, 5, null, 0)],
                payments:
                [
                    new CheckoutPaymentInput("Cash", 20.00m, null),
                    new CheckoutPaymentInput("bKash", 30.00m, "TRX999"),
                ]),
            CancellationToken.None);

        result.Sale.PaidAmount.Should().Be(50.00m);
        result.Sale.ChangeDue.Should().Be(0);
        result.Sale.Payments.Select(payment => payment.Method)
            .Should().BeEquivalentTo("Cash", "bKash");
    }

    [Fact]
    public async Task Concurrent_checkouts_conflict_as_409()
    {
        Guid medicineId = await SeedMedicineAsync(TenantA, "Napa", price: 10.00m);
        await SeedBatchAsync(TenantA, medicineId, "NP-001", quantity: 100, daysToExpiry: 400);

        await using var first = _fixture.CreateAppContext(TenantA);
        await using var second = _fixture.CreateAppContext(TenantA);

        // Prime the first context with a tracked stale batch copy.
        var batchId = await first.MedicineBatches
            .Where(batch => batch.MedicineId == medicineId)
            .Select(batch => batch.Id)
            .FirstAsync();
        await first.MedicineBatches.FirstAsync(batch => batch.Id == batchId);

        await new CheckoutService(second, new NullAccessor()).CheckoutAsync(
            Input(
                items: [new CheckoutItemInput(medicineId, 10, null, 0)],
                payments: [new CheckoutPaymentInput("Cash", 100.00m, null)],
                key: "sale-second"),
            CancellationToken.None);

        var stale = async () => await new CheckoutService(first, new NullAccessor()).CheckoutAsync(
            Input(
                items: [new CheckoutItemInput(medicineId, 10, null, 0)],
                payments: [new CheckoutPaymentInput("Cash", 100.00m, null)],
                key: "sale-first"),
            CancellationToken.None);

        (await stale.Should().ThrowAsync<ConflictException>())
            .Which.Code.Should().Be("sale.concurrent_update");
    }

    [Fact]
    public async Task Failed_checkouts_do_not_burn_invoice_numbers()
    {
        Guid medicineId = await SeedMedicineAsync(TenantA, "Napa", price: 10.00m);
        await SeedBatchAsync(TenantA, medicineId, "NP-001", quantity: 5, daysToExpiry: 400);

        await using var db = _fixture.CreateAppContext(TenantA);
        var checkout = new CheckoutService(db, new NullAccessor());

        var shortfall = async () => await checkout.CheckoutAsync(
            Input(
                items: [new CheckoutItemInput(medicineId, 6, null, 0)],
                payments: [new CheckoutPaymentInput("Cash", 60.00m, null)]),
            CancellationToken.None);
        await shortfall.Should().ThrowAsync<BusinessRuleException>();

        var first = await checkout.CheckoutAsync(
            Input(
                items: [new CheckoutItemInput(medicineId, 2, null, 0)],
                payments: [new CheckoutPaymentInput("Cash", 20.00m, null)]),
            CancellationToken.None);

        first.Sale.InvoiceNumber.Should().Be("INV-000001");
    }

    [Fact]
    public async Task Today_summary_uses_the_tenant_timezone_not_utc()
    {
        // Frozen at 2026-01-15T19:00Z, which is 2026-01-16 01:00 in Dhaka (UTC+6, no DST).
        var frozen = new DateTimeOffset(2026, 1, 15, 19, 0, 0, TimeSpan.Zero);

        await using (var setup = _fixture.CreateAppContext(TenantA))
        {
            setup.PharmacyProfiles.Add(new PharmaPOS.Domain.Catalog.PharmacyProfile
            {
                TenantId = TenantA,
                PharmacyName = "Alpha",
                Currency = "BDT",
                DefaultLanguage = "en",
                Timezone = "Asia/Dhaka",
            });

            // 01:00 Dhaka on the 16th: counts for the 16th.
            setup.Sales.Add(TestSale(TenantA, new DateTimeOffset(2026, 1, 15, 19, 0, 0, TimeSpan.Zero), 100.00m));
            // 23:00 Dhaka on the 15th: belongs to the previous local day.
            setup.Sales.Add(TestSale(TenantA, new DateTimeOffset(2026, 1, 15, 17, 0, 0, TimeSpan.Zero), 50.00m));

            await setup.SaveChangesAsync();
        }

        await using var db = _fixture.CreateAppContext(TenantA);
        var summary = await new SaleReader(db, new FrozenTimeProvider(frozen))
            .GetTodaySummaryAsync(CancellationToken.None);

        summary.Date.Should().Be(new DateOnly(2026, 1, 16));
        summary.SaleCount.Should().Be(1);
        summary.TotalAmount.Should().Be(100.00m);
    }

    [Fact]
    public async Task Sales_do_not_leak_across_tenants()
    {
        Guid medicineId = await SeedMedicineAsync(TenantA, "Napa", price: 10.00m);
        await SeedBatchAsync(TenantA, medicineId, "NP-001", quantity: 100, daysToExpiry: 400);

        Guid saleId;
        await using (var alpha = _fixture.CreateAppContext(TenantA))
        {
            saleId = (await new CheckoutService(alpha, new NullAccessor()).CheckoutAsync(
                Input(
                    items: [new CheckoutItemInput(medicineId, 1, null, 0)],
                    payments: [new CheckoutPaymentInput("Cash", 10.00m, null)]),
                CancellationToken.None)).Sale.Id;
        }

        await using var beta = _fixture.CreateAppContext(TenantB);
        var reader = new SaleReader(beta, TimeProvider.System);

        (await reader.FindAsync(saleId, CancellationToken.None)).Should().BeNull();
        (await reader.ListAsync(null, null, 0, 25, CancellationToken.None)).Should().BeEmpty();
        (await reader.GetTodaySummaryAsync(CancellationToken.None)).SaleCount.Should().Be(0);
    }

    private async Task<Guid> SeedMedicineAsync(string tenantId, string brand, decimal price)
    {
        await using var db = _fixture.CreateAppContext(tenantId);

        var medicine = new PharmaPOS.Domain.Catalog.Medicine
        {
            TenantId = tenantId,
            BrandName = brand,
            GenericName = "Test Generic",
            Strength = "100mg",
            DosageForm = "Tablet",
            Unit = "strip",
            PurchasePrice = price - 2.00m,
            SellingPrice = price,
            ReorderLevel = 10,
            RequiresPrescription = false,
            IsActive = true,
        };

        db.Medicines.Add(medicine);
        await db.SaveChangesAsync();

        return medicine.Id;
    }

    private async Task SeedBatchAsync(
        string tenantId, Guid medicineId, string batchNumber, int quantity, int daysToExpiry)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        await using var db = _fixture.CreateAppContext(tenantId);

        await AddBatchAsync(db, medicineId, batchNumber, quantity, today.AddDays(daysToExpiry), 10.00m);
    }

    private async Task<Guid> AddBatchAsync(
        AppDbContext db,
        Guid medicineId,
        string batchNumber,
        int quantity,
        DateOnly expiry,
        decimal price)
    {
        var batch = new PharmaPOS.Domain.Inventory.MedicineBatch
        {
            TenantId = db.TenantInfo?.Id ?? TenantA,
            MedicineId = medicineId,
            BatchNumber = batchNumber,
            PurchasePrice = price,
            SellingPrice = price,
            Quantity = quantity,
            ExpiryDate = expiry,
        };

        db.MedicineBatches.Add(batch);
        await db.SaveChangesAsync();

        return batch.Id;
    }

    private static CheckoutInput Input(
        IReadOnlyList<CheckoutItemInput> items,
        IReadOnlyList<CheckoutPaymentInput> payments,
        string key = "test-key")
        => new(
            Items: items,
            Payments: payments,
            DiscountAmount: 0,
            CustomerId: null,
            CustomerName: null,
            CustomerPhone: null,
            PrescriptionReference: null,
            IdempotencyKey: key);

    private static PharmaPOS.Domain.Sales.Sale TestSale(
        string tenantId, DateTimeOffset occurredAt, decimal total)
        => new()
        {
            TenantId = tenantId,
            InvoiceNumber = $"T-{Guid.NewGuid():N}"[..12],
            OccurredAtUtc = occurredAt,
            Subtotal = total,
            DiscountAmount = 0,
            TotalAmount = total,
            PaidAmount = total,
        };

    private sealed class FrozenTimeProvider(DateTimeOffset frozen) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => frozen;
    }

    private sealed class NullAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}
