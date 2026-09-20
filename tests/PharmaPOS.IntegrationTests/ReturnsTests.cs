using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PharmaPOS.Application.Common.Customers;
using PharmaPOS.Application.Common.Sales;
using PharmaPOS.Domain.Exceptions;
using PharmaPOS.Infrastructure.Customers;
using PharmaPOS.Infrastructure.Persistence;
using PharmaPOS.Infrastructure.Sales;
using Xunit;

namespace PharmaPOS.IntegrationTests;

/// <summary>
/// Proves the Phase 6 return model against PostgreSQL: atomic restock + SALE_RETURN ledger,
/// server-computed pro-rata refunds, returnable-quantity guards, no-restock path,
/// idempotent replay and cross-tenant invisibility. Sales stay immutable.
/// </summary>
public sealed class ReturnsTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string TenantA = "tenant-alpha";
    private const string TenantB = "tenant-beta";

    private readonly PostgresFixture _fixture;

    public ReturnsTests(PostgresFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Return_restocks_and_refunds_pro_rata()
    {
        var medicineId = await SeedMedicineAsync(TenantA, "Napa", price: 10.00m);
        await SeedBatchAsync(TenantA, medicineId, "NP-001", quantity: 100, daysToExpiry: 400);

        Guid saleId;
        Guid saleItemId;
        await using (var db = _fixture.CreateAppContext(TenantA))
        {
            var sale = await new CheckoutService(db, new NullAccessor()).CheckoutAsync(
                new CheckoutInput(
                    Items: [new CheckoutItemInput(medicineId, 4, null, 0)],
                    Payments: [new CheckoutPaymentInput("Cash", 40.00m, null)],
                    DiscountAmount: 0,
                    CustomerId: null,
                    CustomerName: null,
                    CustomerPhone: null,
                    PrescriptionReference: null,
                    IdempotencyKey: "sale-1"),
                CancellationToken.None);

            saleId = sale.Sale.Id;
            saleItemId = sale.Sale.Items.Single().Id;
            (await db.MedicineBatches.SingleAsync()).Quantity.Should().Be(96);
        }

        await using (var db = _fixture.CreateAppContext(TenantA))
        {
            var returns = new ReturnService(db, new NullAccessor(), new ReturnReader(db));
            var result = await returns.ProcessAsync(
                new ReturnInput(
                    SaleId: saleId,
                    Items: [new ReturnItemInput(saleItemId, 1)],
                    Reason: "Wrong item",
                    Restock: true,
                    RefundMethod: "Cash",
                    RefundReference: null,
                    IdempotencyKey: "ret-1"),
                CancellationToken.None);

            result.Replayed.Should().BeFalse();
            result.Return.RefundAmount.Should().Be(10.00m);

            (await db.MedicineBatches.SingleAsync()).Quantity.Should().Be(97);

            var movements = await db.InventoryMovements
                .OrderBy(movement => movement.OccurredAtUtc)
                .ToListAsync();
            movements.Should().HaveCount(2);
            movements.Last().Type.Should().Be("SALE_RETURN");
            movements.Last().QuantityChange.Should().Be(1);
            movements.Last().QuantityAfter.Should().Be(97);
        }
    }

    [Fact]
    public async Task Return_without_restock_leaves_stock_untouched()
    {
        var medicineId = await SeedMedicineAsync(TenantA, "Napa", price: 10.00m);
        await SeedBatchAsync(TenantA, medicineId, "NP-001", quantity: 100, daysToExpiry: 400);

        Guid saleId;
        Guid saleItemId;
        await using (var db = _fixture.CreateAppContext(TenantA))
        {
            var sale = await new CheckoutService(db, new NullAccessor()).CheckoutAsync(
                Checkout("sale-1", medicineId, 2), CancellationToken.None);
            saleId = sale.Sale.Id;
            saleItemId = sale.Sale.Items.Single().Id;
        }

        await using (var db = _fixture.CreateAppContext(TenantA))
        {
            var result = await new ReturnService(db, new NullAccessor(), new ReturnReader(db))
                .ProcessAsync(
                    new ReturnInput(saleId, [new ReturnItemInput(saleItemId, 1)],
                        "Damaged", Restock: false, "Cash", null, "ret-dmg"),
                    CancellationToken.None);

            result.Return.Restock.Should().BeFalse();
            result.Return.RefundAmount.Should().Be(10.00m);
            (await db.MedicineBatches.SingleAsync()).Quantity.Should().Be(98);
            (await db.InventoryMovements.CountAsync()).Should().Be(1);
        }
    }

    [Fact]
    public async Task Cannot_return_more_than_sold_or_twice()
    {
        var medicineId = await SeedMedicineAsync(TenantA, "Napa", price: 10.00m);
        await SeedBatchAsync(TenantA, medicineId, "NP-001", quantity: 100, daysToExpiry: 400);

        Guid saleId;
        Guid saleItemId;
        await using (var db = _fixture.CreateAppContext(TenantA))
        {
            var sale = await new CheckoutService(db, new NullAccessor()).CheckoutAsync(
                Checkout("sale-1", medicineId, 2), CancellationToken.None);
            saleId = sale.Sale.Id;
            saleItemId = sale.Sale.Items.Single().Id;
        }

        await using var dbReturn = _fixture.CreateAppContext(TenantA);
        var returns = new ReturnService(dbReturn, new NullAccessor(), new ReturnReader(dbReturn));

        var over = async () => await returns.ProcessAsync(
            new ReturnInput(saleId, [new ReturnItemInput(saleItemId, 3)],
                "Too many", true, "Cash", null, "ret-over"),
            CancellationToken.None);
        (await over.Should().ThrowAsync<BusinessRuleException>())
            .Which.Code.Should().Be("return.quantity_exceeded");

        await returns.ProcessAsync(
            new ReturnInput(saleId, [new ReturnItemInput(saleItemId, 2)],
                "Full", true, "Cash", null, "ret-full"),
            CancellationToken.None);

        var second = async () => await returns.ProcessAsync(
            new ReturnInput(saleId, [new ReturnItemInput(saleItemId, 1)],
                "Again", true, "Cash", null, "ret-again"),
            CancellationToken.None);
        (await second.Should().ThrowAsync<BusinessRuleException>())
            .Which.Code.Should().Be("return.quantity_exceeded");
    }

    [Fact]
    public async Task Replay_returns_the_stored_return_without_side_effects()
    {
        var medicineId = await SeedMedicineAsync(TenantA, "Napa", price: 10.00m);
        await SeedBatchAsync(TenantA, medicineId, "NP-001", quantity: 100, daysToExpiry: 400);

        await using var db = _fixture.CreateAppContext(TenantA);
        var checkout = new CheckoutService(db, new NullAccessor());
        var sale = await checkout.CheckoutAsync(
            Checkout("sale-1", medicineId, 2), CancellationToken.None);
        var saleItemId = sale.Sale.Items.Single().Id;

        var returns = new ReturnService(db, new NullAccessor(), new ReturnReader(db));
        var input = new ReturnInput(
            sale.Sale.Id, [new ReturnItemInput(saleItemId, 1)],
            "Change of mind", true, "Cash", null, "ret-key-1");

        var first = await returns.ProcessAsync(input, CancellationToken.None);
        first.Replayed.Should().BeFalse();

        var replay = await returns.ProcessAsync(input, CancellationToken.None);
        replay.Replayed.Should().BeTrue();
        replay.Return.Id.Should().Be(first.Return.Id);

        (await db.MedicineBatches.SingleAsync()).Quantity.Should().Be(99);
        (await db.InventoryMovements.CountAsync()).Should().Be(2);
        (await db.SaleReturns.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Checkout_links_a_customer_and_returnable_tracks_refunds()
    {
        await using (var setup = _fixture.CreateAppContext(TenantA))
        {
            var customer = await new CustomerRepository(setup).CreateAsync(
                new CustomerInput("Loyal", "01711111111", null, null, null),
                CancellationToken.None);

            var medicineId = await SeedMedicineAsync(TenantA, "Napa", price: 10.00m);
            await SeedBatchAsync(TenantA, medicineId, "NP-001", quantity: 100, daysToExpiry: 400);

            var sale = await new CheckoutService(setup, new NullAccessor()).CheckoutAsync(
                new CheckoutInput(
                    Items: [new CheckoutItemInput(medicineId, 2, null, 0)],
                    Payments: [new CheckoutPaymentInput("Cash", 20.00m, null)],
                    DiscountAmount: 0,
                    CustomerId: customer.Id,
                    CustomerName: null,
                    CustomerPhone: null,
                    PrescriptionReference: null,
                    IdempotencyKey: "sale-cust"),
                CancellationToken.None);

            sale.Sale.CustomerId.Should().Be(customer.Id);
            sale.Sale.CustomerName.Should().Be("Loyal");

            var history = await new CustomerRepository(setup)
                .FindWithHistoryAsync(customer.Id, CancellationToken.None);
            history!.RecentSales.Should().ContainSingle()
                .Which.InvoiceNumber.Should().Be(sale.Sale.InvoiceNumber);

            var reader = new ReturnReader(setup);
            var before = await reader.GetReturnableAsync(sale.Sale.Id, CancellationToken.None);
            before!.Items.Single().ReturnableQuantity.Should().Be(2);

            await new ReturnService(setup, new NullAccessor(), reader).ProcessAsync(
                new ReturnInput(sale.Sale.Id,
                    [new ReturnItemInput(sale.Sale.Items.Single().Id, 1)],
                    "Partial", true, "Cash", null, "ret-part"),
                CancellationToken.None);

            var after = await reader.GetReturnableAsync(sale.Sale.Id, CancellationToken.None);
            after!.Items.Single().ReturnableQuantity.Should().Be(1);
            after.AlreadyRefundedAmount.Should().Be(10.00m);
        }
    }

    [Fact]
    public async Task Returns_do_not_leak_across_tenants()
    {
        var medicineId = await SeedMedicineAsync(TenantA, "Napa", price: 10.00m);
        await SeedBatchAsync(TenantA, medicineId, "NP-001", quantity: 100, daysToExpiry: 400);

        Guid returnId;
        Guid saleId;
        await using (var alpha = _fixture.CreateAppContext(TenantA))
        {
            var sale = await new CheckoutService(alpha, new NullAccessor()).CheckoutAsync(
                Checkout("sale-1", medicineId, 1), CancellationToken.None);
            saleId = sale.Sale.Id;
            returnId = (await new ReturnService(alpha, new NullAccessor(), new ReturnReader(alpha))
                .ProcessAsync(
                    new ReturnInput(saleId,
                        [new ReturnItemInput(sale.Sale.Items.Single().Id, 1)],
                        "R", true, "Cash", null, "ret-1"),
                    CancellationToken.None)).Return.Id;
        }

        await using var beta = _fixture.CreateAppContext(TenantB);
        var reader = new ReturnReader(beta);
        (await reader.FindAsync(returnId, CancellationToken.None)).Should().BeNull();
        (await reader.ListAsync(null, 0, 25, CancellationToken.None)).Should().BeEmpty();
        (await reader.GetReturnableAsync(saleId, CancellationToken.None)).Should().BeNull();
    }

    private static CheckoutInput Checkout(string key, Guid medicineId, int quantity)
        => new(
            Items: [new CheckoutItemInput(medicineId, quantity, null, 0)],
            Payments: [new CheckoutPaymentInput("Cash", quantity * 10.00m, null)],
            DiscountAmount: 0,
            CustomerId: null,
            CustomerName: null,
            CustomerPhone: null,
            PrescriptionReference: null,
            IdempotencyKey: key);

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

        db.MedicineBatches.Add(new PharmaPOS.Domain.Inventory.MedicineBatch
        {
            TenantId = tenantId,
            MedicineId = medicineId,
            BatchNumber = batchNumber,
            PurchasePrice = 8.00m,
            SellingPrice = 10.00m,
            Quantity = quantity,
            ExpiryDate = today.AddDays(daysToExpiry),
        });
        await db.SaveChangesAsync();
    }

    private sealed class NullAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}
