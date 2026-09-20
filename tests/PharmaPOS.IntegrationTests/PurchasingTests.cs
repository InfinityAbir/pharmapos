using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PharmaPOS.Application.Common.Purchasing;
using PharmaPOS.Domain.Exceptions;
using PharmaPOS.Infrastructure.Persistence;
using PharmaPOS.Infrastructure.Purchasing;
using Xunit;

namespace PharmaPOS.IntegrationTests;

/// <summary>
/// Proves the Phase 4 purchasing model against PostgreSQL: supplier guards, server-computed
/// totals, draft lifecycle, atomic idempotent receiving with ledger rows, transition guards,
/// concurrency mapping, and cross-tenant invisibility.
/// </summary>
public sealed class PurchasingTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string TenantA = "tenant-alpha";
    private const string TenantB = "tenant-beta";

    private readonly PostgresFixture _fixture;

    public PurchasingTests(PostgresFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Supplier_crud_with_duplicate_and_in_use_guards()
    {
        await using var db = _fixture.CreateAppContext(TenantA);
        var suppliers = new SupplierRepository(db);

        var created = await suppliers.CreateAsync(
            new SupplierInput("Square Distributor", "Square Pharma", null, null, null, null, "Net 30"),
            CancellationToken.None);
        created.IsActive.Should().BeTrue();

        var duplicate = async () => await suppliers.CreateAsync(
            new SupplierInput("Square Distributor", null, null, null, null, null, null),
            CancellationToken.None);
        (await duplicate.Should().ThrowAsync<ConflictException>())
            .Which.Code.Should().Be("supplier.duplicate");

        var renamed = await suppliers.UpdateAsync(
            created.Id,
            new SupplierInput("Square Depot", null, null, null, null, null, null),
            CancellationToken.None);
        renamed.Name.Should().Be("Square Depot");

        var deactivated = await suppliers.SetActiveAsync(created.Id, false, CancellationToken.None);
        deactivated.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Supplier_with_purchases_cannot_be_deleted()
    {
        Guid supplierId;
        Guid medicineId;

        await using (var setup = _fixture.CreateAppContext(TenantA))
        {
            supplierId = (await new SupplierRepository(setup).CreateAsync(
                new SupplierInput("Square", null, null, null, null, null, null),
                CancellationToken.None)).Id;
            medicineId = await SeedMedicineAsync(TenantA, "Napa");
        }

        await using (var writer = _fixture.CreateAppContext(TenantA))
        {
            await new PurchaseRepository(writer, new NullAccessor()).CreateDraftAsync(
                Draft(supplierId, medicineId), CancellationToken.None);
        }

        await using var reader = _fixture.CreateAppContext(TenantA);
        var act = async () => await new SupplierRepository(reader)
            .DeleteAsync(supplierId, CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>())
            .Which.Code.Should().Be("supplier.in_use");
    }

    [Fact]
    public async Task Draft_totals_are_computed_server_side()
    {
        Guid supplierId;
        Guid medicineId;

        await using (var setup = _fixture.CreateAppContext(TenantA))
        {
            supplierId = (await new SupplierRepository(setup).CreateAsync(
                new SupplierInput("Square", null, null, null, null, null, null),
                CancellationToken.None)).Id;
            medicineId = await SeedMedicineAsync(TenantA, "Napa");
        }

        await using var db = _fixture.CreateAppContext(TenantA);
        var draft = await new PurchaseRepository(db, new NullAccessor()).CreateDraftAsync(
            new PurchaseInput(
                SupplierId: supplierId,
                PurchaseDate: DateOnly.FromDateTime(DateTime.UtcNow.Date),
                SupplierInvoiceNumber: "SQ-1",
                DiscountAmount: 5.00m,
                TaxAmount: 2.00m,
                Notes: null,
                Items:
                [
                    Item(medicineId, "NP-001", quantity: 10, unitPrice: 8.00m, discount: 5.00m),
                    Item(medicineId, "NP-002", quantity: 5, unitPrice: 8.00m, discount: 0),
                ]),
            CancellationToken.None);

        // (10*8-5) + (5*8) = 115 subtotal; 115 - 5 + 2 = 112 total.
        draft.Subtotal.Should().Be(115.00m);
        draft.TotalAmount.Should().Be(112.00m);
        draft.Status.Should().Be("Draft");
        draft.Items.Should().HaveCount(2);
        draft.Items.Select(item => item.LineTotal).Should()
            .BeEquivalentTo([75.00m, 40.00m]);
    }

    [Fact]
    public async Task Receive_posts_batches_and_movements_atomically_and_replays_by_key()
    {
        Guid supplierId;
        Guid medicineId;

        await using (var setupSuppliers = _fixture.CreateAppContext(TenantA))
        {
            supplierId = (await new SupplierRepository(setupSuppliers).CreateAsync(
                new SupplierInput("Square", null, null, null, null, null, null),
                CancellationToken.None)).Id;
            medicineId = await SeedMedicineAsync(TenantA, "Napa");
        }

        Guid purchaseId;
        await using (var setup = _fixture.CreateAppContext(TenantA))
        {
            purchaseId = (await new PurchaseRepository(setup, new NullAccessor()).CreateDraftAsync(
                Draft(supplierId, medicineId), CancellationToken.None)).Id;
        }

        await using var db = _fixture.CreateAppContext(TenantA);
        var purchases = new PurchaseRepository(db, new NullAccessor());

        var first = await purchases.ReceiveAsync(purchaseId, "key-1", CancellationToken.None);
        first.Replayed.Should().BeFalse();
        first.Purchase.Status.Should().Be("Received");
        first.Purchase.ReceivedAtUtc.Should().NotBeNull();

        var batch = await db.MedicineBatches.SingleAsync();
        batch.Quantity.Should().Be(100);
        batch.SupplierName.Should().Be("Square");

        var movements = await db.InventoryMovements.ToListAsync();
        movements.Should().ContainSingle()
            .Which.Should().Match<PharmaPOS.Domain.Inventory.InventoryMovement>(movement =>
                movement.Type == "PURCHASE_RECEIVED"
                && movement.QuantityChange == 100
                && movement.QuantityAfter == 100);

        // Same key again: stored outcome, no new stock, no new movements.
        var replay = await purchases.ReceiveAsync(purchaseId, "key-1", CancellationToken.None);
        replay.Replayed.Should().BeTrue();
        replay.Purchase.Status.Should().Be("Received");

        (await db.MedicineBatches.SingleAsync()).Quantity.Should().Be(100);
        (await db.InventoryMovements.CountAsync()).Should().Be(1);

        // Same purchase, new key: already received, not a replay.
        var again = async () => await purchases.ReceiveAsync(purchaseId, "key-2", CancellationToken.None);
        (await again.Should().ThrowAsync<BusinessRuleException>())
            .Which.Code.Should().Be("purchase.not_draft");
    }

    [Fact]
    public async Task Received_and_cancelled_documents_are_immutable()
    {
        Guid supplierId;
        Guid medicineId;

        await using (var db = _fixture.CreateAppContext(TenantA))
        {
            supplierId = (await new SupplierRepository(db).CreateAsync(
                new SupplierInput("Square", null, null, null, null, null, null),
                CancellationToken.None)).Id;
            medicineId = await SeedMedicineAsync(TenantA, "Napa");
        }

        Guid receivedId;
        Guid draftId;
        await using (var setup = _fixture.CreateAppContext(TenantA))
        {
            var setupPurchases = new PurchaseRepository(setup, new NullAccessor());
            receivedId = (await setupPurchases.CreateDraftAsync(
                Draft(supplierId, medicineId), CancellationToken.None)).Id;
            draftId = (await setupPurchases.CreateDraftAsync(
                Draft(supplierId, medicineId), CancellationToken.None)).Id;

            await setupPurchases.ReceiveAsync(receivedId, "key-r", CancellationToken.None);
            await setupPurchases.CancelAsync(draftId, CancellationToken.None);
        }

        await using var reader = _fixture.CreateAppContext(TenantA);
        var purchases = new PurchaseRepository(reader, new NullAccessor());

        var updateReceived = async () => await purchases.UpdateDraftAsync(
            receivedId, Draft(supplierId, medicineId), CancellationToken.None);
        (await updateReceived.Should().ThrowAsync<BusinessRuleException>())
            .Which.Code.Should().Be("purchase.not_draft");

        var cancelReceived = async () => await purchases.CancelAsync(receivedId, CancellationToken.None);
        (await cancelReceived.Should().ThrowAsync<BusinessRuleException>())
            .Which.Code.Should().Be("purchase.not_draft");

        var cancelCancelled = async () => await purchases.CancelAsync(draftId, CancellationToken.None);
        (await cancelCancelled.Should().ThrowAsync<BusinessRuleException>())
            .Which.Code.Should().Be("purchase.not_draft");
    }

    [Fact]
    public async Task Concurrent_receives_conflict_as_409()
    {
        Guid supplierId;
        Guid medicineId;

        await using (var db = _fixture.CreateAppContext(TenantA))
        {
            supplierId = (await new SupplierRepository(db).CreateAsync(
                new SupplierInput("Square", null, null, null, null, null, null),
                CancellationToken.None)).Id;
            medicineId = await SeedMedicineAsync(TenantA, "Napa");
        }

        Guid purchaseId;
        await using (var seed = _fixture.CreateAppContext(TenantA))
        {
            purchaseId = (await new PurchaseRepository(seed, new NullAccessor()).CreateDraftAsync(
                Draft(supplierId, medicineId), CancellationToken.None)).Id;
        }

        await using var first = _fixture.CreateAppContext(TenantA);
        await using var second = _fixture.CreateAppContext(TenantA);

        // Prime the first context with a tracked stale copy of the draft.
        await first.Purchases.FirstAsync(purchase => purchase.Id == purchaseId);

        await new PurchaseRepository(second, new NullAccessor())
            .ReceiveAsync(purchaseId, "key-second", CancellationToken.None);

        var stale = async () => await new PurchaseRepository(first, new NullAccessor())
            .ReceiveAsync(purchaseId, "key-first", CancellationToken.None);

        (await stale.Should().ThrowAsync<ConflictException>())
            .Which.Code.Should().Be("purchase.concurrent_update");
    }

    [Fact]
    public async Task Purchases_do_not_leak_across_tenants()
    {
        Guid supplierId;
        Guid medicineId;
        Guid purchaseId;

        await using (var alpha = _fixture.CreateAppContext(TenantA))
        {
            supplierId = (await new SupplierRepository(alpha).CreateAsync(
                new SupplierInput("Square", null, null, null, null, null, null),
                CancellationToken.None)).Id;
            medicineId = await SeedMedicineAsync(TenantA, "Napa");
            purchaseId = (await new PurchaseRepository(alpha, new NullAccessor()).CreateDraftAsync(
                Draft(supplierId, medicineId), CancellationToken.None)).Id;
        }

        await using var beta = _fixture.CreateAppContext(TenantB);

        (await new PurchaseRepository(beta, new NullAccessor())
            .FindAsync(purchaseId, CancellationToken.None)).Should().BeNull();
        (await new SupplierRepository(beta)
            .FindAsync(supplierId, CancellationToken.None)).Should().BeNull();
        (await new PurchaseRepository(beta, new NullAccessor())
            .ListAsync(null, null, 0, 25, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task Unknown_supplier_or_medicine_is_rejected()
    {
        await using var db = _fixture.CreateAppContext(TenantA);
        var purchases = new PurchaseRepository(db, new NullAccessor());

        var badSupplier = async () => await purchases.CreateDraftAsync(
            Draft(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);
        (await badSupplier.Should().ThrowAsync<NotFoundException>())
            .Which.Code.Should().Be("supplier.not_found");

        Guid supplierId = (await new SupplierRepository(db).CreateAsync(
            new SupplierInput("Square", null, null, null, null, null, null),
            CancellationToken.None)).Id;

        var badMedicine = async () => await purchases.CreateDraftAsync(
            Draft(supplierId, Guid.NewGuid()), CancellationToken.None);
        (await badMedicine.Should().ThrowAsync<NotFoundException>())
            .Which.Code.Should().Be("medicine.not_found");
    }

    private async Task<Guid> SeedMedicineAsync(string tenantId, string brand)
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
            PurchasePrice = 5.00m,
            SellingPrice = 10.00m,
            ReorderLevel = 10,
            RequiresPrescription = false,
            IsActive = true,
        };

        db.Medicines.Add(medicine);
        await db.SaveChangesAsync();

        return medicine.Id;
    }

    private static PurchaseInput Draft(Guid supplierId, Guid medicineId) => new(
        SupplierId: supplierId,
        PurchaseDate: DateOnly.FromDateTime(DateTime.UtcNow.Date),
        SupplierInvoiceNumber: null,
        DiscountAmount: 0,
        TaxAmount: 0,
        Notes: null,
        Items: [Item(medicineId, "NP-001", quantity: 100, unitPrice: 8.00m, discount: 0)]);

    private static PurchaseItemInput Item(
        Guid medicineId,
        string batch,
        int quantity,
        decimal unitPrice,
        decimal discount) => new(
            MedicineId: medicineId,
            BatchNumber: batch,
            ManufactureDate: null,
            ExpiryDate: DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(400)),
            Quantity: quantity,
            UnitPrice: unitPrice,
            DiscountAmount: discount,
            SellingPrice: null);

    private sealed class NullAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}
