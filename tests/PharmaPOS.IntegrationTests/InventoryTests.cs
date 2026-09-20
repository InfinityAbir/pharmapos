using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PharmaPOS.Application.Common.Inventory;
using PharmaPOS.Application.Features.Inventory;
using PharmaPOS.Domain.Exceptions;
using PharmaPOS.Infrastructure.Inventory;
using PharmaPOS.Infrastructure.Persistence;
using Xunit;

namespace PharmaPOS.IntegrationTests;

/// <summary>
/// Proves the Phase 3 inventory model against PostgreSQL: batch receipts (new + top-up), guarded
/// adjustments, expiry, derived stock totals, alerts, the append-only ledger, optimistic
/// concurrency mapping to 409, and cross-tenant invisibility.
/// </summary>
public sealed class InventoryTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string TenantA = "tenant-alpha";
    private const string TenantB = "tenant-beta";

    private readonly PostgresFixture _fixture;

    public InventoryTests(PostgresFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Receive_creates_a_batch_with_an_opening_movement()
    {
        Guid medicineId = await SeedMedicineAsync(TenantA, "Napa", reorderLevel: 50);

        await using var db = _fixture.CreateAppContext(TenantA);
        var batch = await new BatchRepository(db, new TestHttpContextAccessor())
            .ReceiveAsync(Input(medicineId, "NP-001", quantity: 100), CancellationToken.None);

        batch.Quantity.Should().Be(100);
        batch.BatchNumber.Should().Be("NP-001");

        var movements = await new MovementReader(db)
            .ListAsync(medicineId, null, 0, 10, CancellationToken.None);

        movements.Should().ContainSingle()
            .Which.Should().Match<MovementDto>(movement =>
                movement.Type == "PURCHASE_RECEIVED"
                && movement.QuantityChange == 100
                && movement.QuantityAfter == 100);
    }

    [Fact]
    public async Task Receive_tops_up_an_existing_batch_number()
    {
        Guid medicineId = await SeedMedicineAsync(TenantA, "Napa", reorderLevel: 50);

        await using var db = _fixture.CreateAppContext(TenantA);
        var batches = new BatchRepository(db, new TestHttpContextAccessor());

        await batches.ReceiveAsync(Input(medicineId, "NP-001", quantity: 100), CancellationToken.None);
        var topped = await batches.ReceiveAsync(
            Input(medicineId, "NP-001", quantity: 25), CancellationToken.None);

        topped.Quantity.Should().Be(125);

        var listed = await batches.ListAsync(medicineId, null, true, CancellationToken.None);
        listed.Should().ContainSingle();
    }

    [Fact]
    public async Task Receive_rejects_mismatched_dates_on_top_up()
    {
        Guid medicineId = await SeedMedicineAsync(TenantA, "Napa", reorderLevel: 50);

        await using var db = _fixture.CreateAppContext(TenantA);
        var batches = new BatchRepository(db, new TestHttpContextAccessor());

        await batches.ReceiveAsync(Input(medicineId, "NP-001", quantity: 100), CancellationToken.None);

        var act = async () => await batches.ReceiveAsync(
            Input(medicineId, "NP-001", quantity: 25)
                with { ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(900)) },
            CancellationToken.None);

        (await act.Should().ThrowAsync<BusinessRuleException>())
            .Which.Code.Should().Be("batch.date_mismatch");
    }

    [Fact]
    public async Task Adjust_moves_stock_both_ways_and_never_below_zero()
    {
        Guid medicineId = await SeedMedicineAsync(TenantA, "Napa", reorderLevel: 50);

        await using var db = _fixture.CreateAppContext(TenantA);
        var batches = new BatchRepository(db, new TestHttpContextAccessor());

        var received = await batches.ReceiveAsync(
            Input(medicineId, "NP-001", quantity: 100), CancellationToken.None);

        var down = await batches.AdjustAsync(received.Id, -30, "damaged", CancellationToken.None);
        down.Quantity.Should().Be(70);

        var up = await batches.AdjustAsync(received.Id, 5, "recount", CancellationToken.None);
        up.Quantity.Should().Be(75);

        var overdraft = async () => await batches.AdjustAsync(
            received.Id, -76, "too much", CancellationToken.None);

        (await overdraft.Should().ThrowAsync<BusinessRuleException>())
            .Which.Code.Should().Be("stock.insufficient");

        var movements = await new MovementReader(db)
            .ListAsync(null, received.Id, 0, 10, CancellationToken.None);

        movements.Select(movement => movement.Type).Should().BeEquivalentTo(
            "ADJUSTMENT_IN", "ADJUSTMENT_OUT", "PURCHASE_RECEIVED");
        movements.First().QuantityAfter.Should().Be(75);
    }

    [Fact]
    public async Task Expire_zeroes_the_batch_with_a_ledger_row()
    {
        Guid medicineId = await SeedMedicineAsync(TenantA, "Napa", reorderLevel: 50);

        await using var db = _fixture.CreateAppContext(TenantA);
        var batches = new BatchRepository(db, new TestHttpContextAccessor());

        var received = await batches.ReceiveAsync(
            Input(medicineId, "NP-001", quantity: 40), CancellationToken.None);

        var expired = await batches.ExpireAsync(received.Id, CancellationToken.None);
        expired.Quantity.Should().Be(0);
        expired.IsExpired.Should().BeFalse();

        var movements = await new MovementReader(db)
            .ListAsync(null, received.Id, 0, 10, CancellationToken.None);

        movements.Should().ContainSingle(movement => movement.Type == "EXPIRED")
            .Which.Should().Match<MovementDto>(movement =>
                movement.QuantityChange == -40 && movement.QuantityAfter == 0);

        var again = async () => await batches.ExpireAsync(received.Id, CancellationToken.None);
        (await again.Should().ThrowAsync<BusinessRuleException>())
            .Which.Code.Should().Be("batch.already_depleted");
    }

    [Fact]
    public async Task Stock_totals_derive_from_batches_and_flag_low_stock()
    {
        Guid medicineId = await SeedMedicineAsync(TenantA, "Napa", reorderLevel: 50);

        await using var db = _fixture.CreateAppContext(TenantA);
        var batches = new BatchRepository(db, new TestHttpContextAccessor());
        var stock = new StockReader(db, TimeProvider.System);

        await batches.ReceiveAsync(Input(medicineId, "NP-001", quantity: 20), CancellationToken.None);
        await batches.ReceiveAsync(Input(medicineId, "NP-002", quantity: 15), CancellationToken.None);

        var detail = (await stock.GetMedicineStockAsync(medicineId, CancellationToken.None))!;
        detail.TotalQuantity.Should().Be(35);
        detail.BatchCount.Should().Be(2);
        detail.IsLowStock.Should().BeTrue();

        (await stock.ListLowStockAsync(CancellationToken.None))
            .Should().ContainSingle().Which.MedicineId.Should().Be(medicineId);

        var batch = (await batches.ListAsync(medicineId, null, true, CancellationToken.None)).First();
        await batches.AdjustAsync(batch.Id, 20, "top-up", CancellationToken.None);

        (await stock.GetMedicineStockAsync(medicineId, CancellationToken.None))!
            .IsLowStock.Should().BeFalse();
        (await stock.ListLowStockAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task Expiring_and_expired_lists_use_quantity_and_horizon()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        Guid medicineId = await SeedMedicineAsync(TenantA, "Napa", reorderLevel: 0);

        await using var db = _fixture.CreateAppContext(TenantA);
        var batches = new BatchRepository(db, new TestHttpContextAccessor());
        var stock = new StockReader(db, TimeProvider.System);

        await batches.ReceiveAsync(
            Input(medicineId, "NEAR", quantity: 10) with { ExpiryDate = today.AddDays(10) },
            CancellationToken.None);
        await batches.ReceiveAsync(
            Input(medicineId, "FAR", quantity: 10) with { ExpiryDate = today.AddDays(200) },
            CancellationToken.None);

        (await stock.ListExpiringAsync(30, CancellationToken.None))
            .Should().ContainSingle().Which.BatchNumber.Should().Be("NEAR");
        (await stock.ListExpiredAsync(CancellationToken.None)).Should().BeEmpty();

        var summary = await stock.GetAlertsSummaryAsync(CancellationToken.None);
        summary.ExpiringWithin30DaysCount.Should().Be(1);
        summary.ExpiredBatchCount.Should().Be(0);
        summary.LowStockCount.Should().Be(0);
    }

    [Fact]
    public async Task Concurrent_adjustments_conflict_as_409_not_500()
    {
        Guid medicineId = await SeedMedicineAsync(TenantA, "Napa", reorderLevel: 0);

        Guid batchId;
        await using (var seed = _fixture.CreateAppContext(TenantA))
        {
            batchId = (await new BatchRepository(seed, new TestHttpContextAccessor())
                .ReceiveAsync(Input(medicineId, "NP-001", quantity: 100), CancellationToken.None)).Id;
        }

        await using var first = _fixture.CreateAppContext(TenantA);
        await using var second = _fixture.CreateAppContext(TenantA);

        // Prime the first context with a tracked copy that is about to go stale: the
        // repository's reload then returns this instance (identity map) with the old xmin.
        await first.MedicineBatches.FirstAsync(batch => batch.Id == batchId);

        await new BatchRepository(second, new TestHttpContextAccessor())
            .AdjustAsync(batchId, -10, "second", CancellationToken.None);

        var stale = async () => await new BatchRepository(first, new TestHttpContextAccessor())
            .AdjustAsync(batchId, -10, "first", CancellationToken.None);

        (await stale.Should().ThrowAsync<ConflictException>())
            .Which.Code.Should().Be("batch.concurrent_update");
    }

    [Fact]
    public async Task Batches_do_not_leak_across_tenants()
    {
        Guid medicineId = await SeedMedicineAsync(TenantA, "Napa", reorderLevel: 0);

        Guid batchId;
        await using (var alpha = _fixture.CreateAppContext(TenantA))
        {
            batchId = (await new BatchRepository(alpha, new TestHttpContextAccessor())
                .ReceiveAsync(Input(medicineId, "NP-001", quantity: 100), CancellationToken.None)).Id;
        }

        await using var beta = _fixture.CreateAppContext(TenantB);

        (await new BatchRepository(beta, new TestHttpContextAccessor())
            .FindAsync(batchId, CancellationToken.None)).Should().BeNull();
        (await new StockReader(beta, TimeProvider.System)
            .GetMedicineStockAsync(medicineId, CancellationToken.None)).Should().BeNull();

        var adjust = async () => await new BatchRepository(beta, new TestHttpContextAccessor())
            .AdjustAsync(batchId, -5, "cross-tenant", CancellationToken.None);

        (await adjust.Should().ThrowAsync<NotFoundException>())
            .Which.Code.Should().Be("batch.not_found");
    }

    private async Task<Guid> SeedMedicineAsync(string tenantId, string brand, int reorderLevel)
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
            ReorderLevel = reorderLevel,
            RequiresPrescription = false,
            IsActive = true,
        };

        db.Medicines.Add(medicine);
        await db.SaveChangesAsync();

        return medicine.Id;
    }

    private static ReceiveStockInput Input(Guid medicineId, string batchNumber, int quantity) => new(
        MedicineId: medicineId,
        BatchNumber: batchNumber,
        Quantity: quantity,
        PurchasePrice: null,
        SellingPrice: null,
        ManufactureDate: null,
        ExpiryDate: DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(400)),
        SupplierName: "Test Supplier",
        PurchaseReference: null);

    private sealed class TestHttpContextAccessor : Microsoft.AspNetCore.Http.IHttpContextAccessor
    {
        public Microsoft.AspNetCore.Http.HttpContext? HttpContext { get; set; }
    }
}
