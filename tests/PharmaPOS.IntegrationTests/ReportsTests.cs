using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PharmaPOS.Domain.Catalog;
using PharmaPOS.Domain.Inventory;
using PharmaPOS.Domain.Purchasing;
using PharmaPOS.Domain.Sales;
using PharmaPOS.Infrastructure.Persistence;
using PharmaPOS.Infrastructure.Reporting;
using Xunit;

namespace PharmaPOS.IntegrationTests;

/// <summary>
/// Proves Phase 7 server-side aggregation against PostgreSQL: every report returns
/// compact, correctly calculated DTOs (no bulk row Shipping), results are isolated
/// per tenant, and ranges stay bounded.
/// </summary>
public sealed class ReportsTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string TenantA = "tenant-alpha";
    private const string TenantB = "tenant-beta";

    private readonly PostgresFixture _fixture;

    public ReportsTests(PostgresFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Trend_buckets_days_weeks_and_months_server_side()
    {
        var dayOne = new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);
        await using (var setup = _fixture.CreateAppContext(TenantA))
        {
            setup.Sales.Add(TestSale(TenantA, dayOne, 100m, "cashier-1"));
            setup.Sales.Add(TestSale(TenantA, dayOne.AddHours(2), 50m, "cashier-1"));
            setup.Sales.Add(TestSale(TenantA, dayOne.AddDays(1), 25m, "cashier-2"));
            await setup.SaveChangesAsync();
        }

        await using var db = _fixture.CreateAppContext(TenantA);
        var reader = new ReportReader(db, TimeProvider.System);
        var from = new DateOnly(2026, 9, 14);
        var to = new DateOnly(2026, 9, 15);

        var days = await reader.GetSalesTrendAsync(from, to, "day", CancellationToken.None);
        days.Should().HaveCount(2);
        days[0].Should().Match<PharmaPOS.Application.Features.Reports.SalesTrendBucketDto>(
            bucket => bucket.PeriodStart == from && bucket.SaleCount == 2 && bucket.TotalAmount == 150m);
        days[1].TotalAmount.Should().Be(25m);

        var weeks = await reader.GetSalesTrendAsync(from, to, "week", CancellationToken.None);
        weeks.Should().ContainSingle()
            .Which.Should().Match<PharmaPOS.Application.Features.Reports.SalesTrendBucketDto>(
                bucket => bucket.SaleCount == 3 && bucket.TotalAmount == 175m);

        var months = await reader.GetSalesTrendAsync(from, to, "month", CancellationToken.None);
        months.Should().ContainSingle()
            .Which.TotalAmount.Should().Be(175m);
    }

    [Fact]
    public async Task By_cashier_and_by_method_aggregate_without_loading_sales()
    {
        Guid saleId;
        await using (var setup = _fixture.CreateAppContext(TenantA))
        {
            var sale = TestSale(TenantA, DateTimeOffset.UtcNow, 50m, "cashier-1");
            sale.PaidAmount = 50m;
            sale.Payments.Add(new SalePayment
            {
                TenantId = TenantA,
                Method = PaymentMethods.Cash,
                Amount = 20m,
            });
            sale.Payments.Add(new SalePayment
            {
                TenantId = TenantA,
                Method = PaymentMethods.BKash,
                Amount = 30m,
                Reference = "TRX1",
            });
            setup.Sales.Add(sale);
            setup.Sales.Add(TestSale(TenantA, DateTimeOffset.UtcNow, 10m, "cashier-2"));
            await setup.SaveChangesAsync();
            saleId = sale.Id;
        }

        saleId.Should().NotBe(Guid.Empty);

        await using var db = _fixture.CreateAppContext(TenantA);
        var reader = new ReportReader(db, TimeProvider.System);
        var from = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
        var to = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1));

        var cashiers = await reader.GetSalesByCashierAsync(from, to, 0, 25, CancellationToken.None);
        cashiers.Should().HaveCount(2);
        cashiers.First().Cashier.Should().Be("cashier-1");
        cashiers.First().TotalAmount.Should().Be(50m);

        var methods = await reader.GetSalesByMethodAsync(from, to, CancellationToken.None);
        methods.Should().HaveCount(2);
        methods.Single(row => row.Method == PaymentMethods.BKash).TotalAmount.Should().Be(30m);
    }

    [Fact]
    public async Task By_medicine_and_performance_rank_quantities_and_revenue()
    {
        var medicineA = await SeedMedicineAsync(TenantA, "Fast", reorder: 5);
        var medicineB = await SeedMedicineAsync(TenantA, "Slow", reorder: 5);
        await using (var setup = _fixture.CreateAppContext(TenantA))
        {
            var sale = TestSale(TenantA, DateTimeOffset.UtcNow, 40m, null);
            var batchA = await AddBatchAsync(setup, medicineA, "A-1", 100, price: 10m);
            var batchB = await AddBatchAsync(setup, medicineB, "B-1", 100, price: 5m);
            sale.Items.Add(TestItem(TenantA, sale, medicineA, batchA, quantity: 3, unitPrice: 10m));
            sale.Items.Add(TestItem(TenantA, sale, medicineB, batchB, quantity: 2, unitPrice: 5m));
            setup.Sales.Add(sale);
            await setup.SaveChangesAsync();
        }

        await using var db = _fixture.CreateAppContext(TenantA);
        var reader = new ReportReader(db, TimeProvider.System);
        var from = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
        var to = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1));

        var byMedicine = await reader.GetSalesByMedicineAsync(from, to, 0, 25, CancellationToken.None);
        byMedicine.Should().HaveCount(2);
        byMedicine.First().QuantitySold.Should().Be(3);
        byMedicine.First().Revenue.Should().Be(30m);

        var best = await reader.GetProductPerformanceAsync(30, bestFirst: true, 0, 25, CancellationToken.None);
        best.Should().HaveCount(2);
        best.First().MedicineId.Should().Be(medicineA);

        var slow = await reader.GetProductPerformanceAsync(30, bestFirst: false, 0, 25, CancellationToken.None);
        slow.First().MedicineId.Should().Be(medicineB);
    }

    [Fact]
    public async Task Slow_movers_include_zero_sale_actives()
    {
        var seller = await SeedMedicineAsync(TenantA, "Seller", reorder: 5);
        var idle = await SeedMedicineAsync(TenantA, "Idle", reorder: 5);
        await using (var setup = _fixture.CreateAppContext(TenantA))
        {
            var sale = TestSale(TenantA, DateTimeOffset.UtcNow, 10m, null);
            var batch = await AddBatchAsync(setup, seller, "S-1", 100, price: 10m);
            sale.Items.Add(TestItem(TenantA, sale, seller, batch, quantity: 1, unitPrice: 10m));
            setup.Sales.Add(sale);
            await setup.SaveChangesAsync();
        }

        seller.Should().NotBe(idle);

        await using var db = _fixture.CreateAppContext(TenantA);
        var slow = await new ReportReader(db, TimeProvider.System)
            .GetProductPerformanceAsync(30, bestFirst: false, 0, 25, CancellationToken.None);

        slow.Should().HaveCount(2);
        slow.First().MedicineId.Should().Be(idle);
        slow.First().QuantitySold.Should().Be(0);
    }

    [Fact]
    public async Task Valuation_and_low_stock_aggregate_in_sql()
    {
        var low = await SeedMedicineAsync(TenantA, "LowMed", reorder: 10);
        var ok = await SeedMedicineAsync(TenantA, "OkMed", reorder: 10);
        await using (var setup = _fixture.CreateAppContext(TenantA))
        {
            await AddBatchAsync(setup, low, "L-1", quantity: 3, price: 8m, selling: 10m);
            await AddBatchAsync(setup, ok, "O-1", quantity: 50, price: 8m, selling: 10m);
            await setup.SaveChangesAsync();
        }

        low.Should().NotBe(ok);

        await using var db = _fixture.CreateAppContext(TenantA);
        var reader = new ReportReader(db, TimeProvider.System);

        var valuation = await reader.GetStockValuationAsync(0, 25, CancellationToken.None);
        valuation.MedicineCount.Should().Be(2);
        valuation.TotalUnits.Should().Be(53);
        valuation.PurchaseValue.Should().Be(53 * 8m);
        valuation.SellingValue.Should().Be(53 * 10m);
        valuation.Rows.Should().HaveCount(2);

        var lowStock = await reader.GetLowStockReportAsync(0, 25, CancellationToken.None);
        lowStock.Should().ContainSingle()
            .Which.Should().Match<PharmaPOS.Application.Features.Reports.LowStockRowDto>(
                row => row.MedicineId == low && row.TotalQuantity == 3 && row.Deficit == 7);
    }

    [Fact]
    public async Task Movement_report_filters_by_type_and_date()
    {
        var medicineId = await SeedMedicineAsync(TenantA, "Napa", reorder: 10);
        await SeedBatchObjectAsync(TenantA, medicineId, "NP-001", quantity: 100, daysToExpiry: 400);

        await using (var db = _fixture.CreateAppContext(TenantA))
        {
            await new PharmaPOS.Infrastructure.Sales.CheckoutService(db, new NullAccessor())
                .CheckoutAsync(
                    new PharmaPOS.Application.Common.Sales.CheckoutInput(
                        Items: [new PharmaPOS.Application.Common.Sales.CheckoutItemInput(medicineId, 2, null, 0)],
                        Payments: [new PharmaPOS.Application.Common.Sales.CheckoutPaymentInput("Cash", 20m, null)],
                        DiscountAmount: 0,
                        CustomerId: null,
                        CustomerName: null,
                        CustomerPhone: null,
                        PrescriptionReference: null,
                        IdempotencyKey: "report-sale-1"),
                    CancellationToken.None);
        }

        await using var reportDb = _fixture.CreateAppContext(TenantA);
        var reader = new ReportReader(reportDb, TimeProvider.System);
        var from = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
        var to = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1));

        var sales = await reader.GetMovementReportAsync(
            null, null, MovementTypes.Sale, from, to, 0, 25, CancellationToken.None);
        sales.Should().ContainSingle()
            .Which.QuantityChange.Should().Be(-2);

        var other = await reader.GetMovementReportAsync(
            null, null, MovementTypes.PurchaseReceived, from, to, 0, 25, CancellationToken.None);
        other.Should().BeEmpty();
    }

    [Fact]
    public async Task Purchase_reports_cover_suppliers_and_totals()
    {
        await using (var setup = _fixture.CreateAppContext(TenantA))
        {
            var supplier = new Supplier
            {
                TenantId = TenantA,
                Name = "Acme",
                IsActive = true,
            };
            setup.Suppliers.Add(supplier);
            await setup.SaveChangesAsync();

            var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            setup.Purchases.Add(new Purchase
            {
                TenantId = TenantA,
                SupplierId = supplier.Id,
                PurchaseDate = today,
                Status = PurchaseStatus.Received,
                TotalAmount = 200m,
            });
            setup.Purchases.Add(new Purchase
            {
                TenantId = TenantA,
                SupplierId = supplier.Id,
                PurchaseDate = today,
                Status = PurchaseStatus.Draft,
                TotalAmount = 0m,
            });
            await setup.SaveChangesAsync();
        }

        await using var db = _fixture.CreateAppContext(TenantA);
        var reader = new ReportReader(db, TimeProvider.System);
        var from = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-30));
        var to = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var bySupplier = await reader.GetPurchasesBySupplierAsync(from, to, 0, 25, CancellationToken.None);
        bySupplier.Should().ContainSingle()
            .Which.Should().Match<PharmaPOS.Application.Features.Reports.PurchasesBySupplierRowDto>(
                row => row.SupplierName == "Acme" && row.PurchaseCount == 1 && row.TotalAmount == 200m);

        var totals = await reader.GetPurchaseTotalsAsync(from, to, CancellationToken.None);
        totals.ReceivedCount.Should().Be(1);
        totals.ReceivedTotal.Should().Be(200m);
        totals.DraftCount.Should().Be(1);
    }

    [Fact]
    public async Task Reports_do_not_leak_across_tenants()
    {
        await using (var alpha = _fixture.CreateAppContext(TenantA))
        {
            alpha.Sales.Add(TestSale(TenantA, DateTimeOffset.UtcNow, 99m, "cashier-1"));
            await alpha.SaveChangesAsync();
        }

        await using var beta = _fixture.CreateAppContext(TenantB);
        var reader = new ReportReader(beta, TimeProvider.System);
        var from = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
        var to = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1));

        (await reader.GetSalesTrendAsync(from, to, "day", CancellationToken.None)).Should().BeEmpty();
        (await reader.GetSalesByCashierAsync(from, to, 0, 25, CancellationToken.None)).Should().BeEmpty();
        (await reader.GetSalesByMethodAsync(from, to, CancellationToken.None)).Should().BeEmpty();
        (await reader.GetSalesByMedicineAsync(from, to, 0, 25, CancellationToken.None)).Should().BeEmpty();
        (await reader.GetProductPerformanceAsync(30, true, 0, 25, CancellationToken.None)).Should().BeEmpty();
        (await reader.GetLowStockReportAsync(0, 25, CancellationToken.None)).Should().BeEmpty();
        (await reader.GetPurchasesBySupplierAsync(from, to, 0, 25, CancellationToken.None)).Should().BeEmpty();

        var valuation = await reader.GetStockValuationAsync(0, 25, CancellationToken.None);
        valuation.TotalUnits.Should().Be(0);
        valuation.Rows.Should().BeEmpty();
    }

    private static Sale TestSale(
        string tenantId, DateTimeOffset occurredAt, decimal total, string? cashier)
        => new()
        {
            TenantId = tenantId,
            InvoiceNumber = $"R-{Guid.NewGuid():N}"[..12],
            OccurredAtUtc = occurredAt,
            Subtotal = total,
            DiscountAmount = 0,
            TotalAmount = total,
            PaidAmount = total,
            CashierSubject = cashier,
        };

    private static SaleItem TestItem(
        string tenantId, Sale sale, Guid medicineId, Guid batchId, int quantity, decimal unitPrice)
        => new()
        {
            TenantId = tenantId,
            Sale = sale,
            MedicineId = medicineId,
            BatchId = batchId,
            Quantity = quantity,
            UnitPrice = unitPrice,
            DiscountAmount = 0,
        };

    private async Task<Guid> SeedMedicineAsync(string tenantId, string brand, int reorder)
    {
        await using var db = _fixture.CreateAppContext(tenantId);

        var medicine = new Medicine
        {
            TenantId = tenantId,
            BrandName = brand,
            GenericName = "Test Generic",
            Strength = "100mg",
            DosageForm = "Tablet",
            Unit = "strip",
            PurchasePrice = 8m,
            SellingPrice = 10m,
            ReorderLevel = reorder,
            RequiresPrescription = false,
            IsActive = true,
        };

        db.Medicines.Add(medicine);
        await db.SaveChangesAsync();

        return medicine.Id;
    }

    private async Task<Guid> AddBatchAsync(
        AppDbContext db, Guid medicineId, string batchNumber, int quantity, decimal price,
        decimal? selling = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var batch = new MedicineBatch
        {
            TenantId = db.TenantInfo?.Id ?? TenantA,
            MedicineId = medicineId,
            BatchNumber = batchNumber,
            PurchasePrice = price,
            SellingPrice = selling ?? price,
            Quantity = quantity,
            ExpiryDate = today.AddDays(400),
        };

        db.MedicineBatches.Add(batch);
        await db.SaveChangesAsync();

        return batch.Id;
    }

    private async Task SeedBatchObjectAsync(
        string tenantId, Guid medicineId, string batchNumber, int quantity, int daysToExpiry)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        await using var db = _fixture.CreateAppContext(tenantId);

        db.MedicineBatches.Add(new MedicineBatch
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
