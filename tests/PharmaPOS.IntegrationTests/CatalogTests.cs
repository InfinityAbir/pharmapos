using FluentAssertions;
using PharmaPOS.Application.Common.Catalog;
using PharmaPOS.Application.Features.Catalog;
using PharmaPOS.Domain.Exceptions;
using PharmaPOS.Infrastructure.Catalog;
using PharmaPOS.Infrastructure.Persistence;
using Xunit;

namespace PharmaPOS.IntegrationTests;

/// <summary>
/// Proves the Phase 2 catalog model against PostgreSQL: tenant-scoped medicines with per-tenant
/// barcode uniqueness, category/manufacturer referential guards, cross-tenant invisibility, and
/// the single-row pharmacy profile.
/// </summary>
public sealed class CatalogTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string TenantA = "tenant-alpha";
    private const string TenantB = "tenant-beta";

    private readonly PostgresFixture _fixture;

    public CatalogTests(PostgresFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Medicine_crud_round_trip_with_search_and_barcode_lookup()
    {
        Guid categoryId;
        Guid manufacturerId;
        Guid medicineId;

        await using (var db = _fixture.CreateAppContext(TenantA))
        {
            var categories = new CategoryRepository(db);
            var manufacturers = new ManufacturerRepository(db);
            var medicines = new MedicineRepository(db);

            categoryId = (await categories.CreateAsync("Analgesic", null, CancellationToken.None)).Id;
            manufacturerId = (await manufacturers
                .CreateAsync("Square Pharmaceuticals", "Bangladesh", CancellationToken.None)).Id;

            var created = await medicines.CreateAsync(
                Input(barcode: "8941154100017", categoryId: categoryId, manufacturerId: manufacturerId),
                CancellationToken.None);
            medicineId = created.Id;

            created.ManufacturerName.Should().Be("Square Pharmaceuticals");
            created.CategoryName.Should().Be("Analgesic");
            created.IsActive.Should().BeTrue();
        }

        await using (var db = _fixture.CreateAppContext(TenantA))
        {
            var medicines = new MedicineRepository(db);

            (await medicines.FindByIdAsync(medicineId, CancellationToken.None))
                .Should().NotBeNull();

            (await medicines.FindByBarcodeAsync("8941154100017", CancellationToken.None))
                .Should().NotBeNull();

            (await medicines.SearchAsync("napa", null, null, null, 0, 25, CancellationToken.None))
                .Should().ContainSingle();
            (await medicines.SearchAsync("PARACETAMOL", null, null, null, 0, 25, CancellationToken.None))
                .Should().ContainSingle();
            (await medicines.SearchAsync("square", null, null, null, 0, 25, CancellationToken.None))
                .Should().ContainSingle();
            (await medicines.SearchAsync("no-such-medicine", null, null, null, 0, 25, CancellationToken.None))
                .Should().BeEmpty();

            var updated = await medicines.UpdateAsync(
                medicineId,
                Input(barcode: "8941154100017", categoryId: categoryId, manufacturerId: manufacturerId)
                    with { SellingPrice = 14.00m },
                CancellationToken.None);
            updated.SellingPrice.Should().Be(14.00m);

            var deactivated = await medicines.SetActiveAsync(medicineId, false, CancellationToken.None);
            deactivated.IsActive.Should().BeFalse();

            (await medicines.SearchAsync(null, null, null, isActive: true, 0, 25, CancellationToken.None))
                .Should().BeEmpty();
            (await medicines.CountAsync(CancellationToken.None)).Should().Be(1);
        }
    }

    [Fact]
    public async Task Barcodes_are_unique_per_tenant_but_reusable_across_tenants()
    {
        await using (var alpha = _fixture.CreateAppContext(TenantA))
        {
            await new MedicineRepository(alpha).CreateAsync(
                Input(barcode: "8941154100017"), CancellationToken.None);

            var duplicate = async () => await new MedicineRepository(alpha).CreateAsync(
                Input(barcode: "8941154100017"), CancellationToken.None);

            (await duplicate.Should().ThrowAsync<ConflictException>())
                .Which.Code.Should().Be("medicine.barcode.duplicate");
        }

        await using var beta = _fixture.CreateAppContext(TenantB);
        var act = async () => await new MedicineRepository(beta).CreateAsync(
            Input(barcode: "8941154100017"), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Medicines_do_not_leak_across_tenants()
    {
        Guid medicineId;
        await using (var alpha = _fixture.CreateAppContext(TenantA))
        {
            medicineId = (await new MedicineRepository(alpha).CreateAsync(
                Input(barcode: "8941154100017"), CancellationToken.None)).Id;
        }

        await using var beta = _fixture.CreateAppContext(TenantB);
        var medicines = new MedicineRepository(beta);

        (await medicines.FindByIdAsync(medicineId, CancellationToken.None)).Should().BeNull();
        (await medicines.FindByBarcodeAsync("8941154100017", CancellationToken.None)).Should().BeNull();
        (await medicines.SearchAsync("napa", null, null, null, 0, 25, CancellationToken.None))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Cross_tenant_category_reference_is_rejected()
    {
        Guid foreignCategoryId;
        await using (var alpha = _fixture.CreateAppContext(TenantA))
        {
            foreignCategoryId = (await new CategoryRepository(alpha)
                .CreateAsync("Analgesic", null, CancellationToken.None)).Id;
        }

        await using var beta = _fixture.CreateAppContext(TenantB);
        var act = async () => await new MedicineRepository(beta).CreateAsync(
            Input(barcode: null, categoryId: foreignCategoryId), CancellationToken.None);

        (await act.Should().ThrowAsync<NotFoundException>())
            .Which.Code.Should().Be("category.not_found");
    }

    [Fact]
    public async Task Duplicate_category_and_manufacturer_names_are_rejected_per_tenant()
    {
        await using var db = _fixture.CreateAppContext(TenantA);
        var categories = new CategoryRepository(db);
        var manufacturers = new ManufacturerRepository(db);

        await categories.CreateAsync("Analgesic", null, CancellationToken.None);
        var duplicateCategory = async () => await categories
            .CreateAsync("Analgesic", null, CancellationToken.None);
        (await duplicateCategory.Should().ThrowAsync<ConflictException>())
            .Which.Code.Should().Be("category.duplicate");

        await manufacturers.CreateAsync("Square", "Bangladesh", CancellationToken.None);
        var duplicateManufacturer = async () => await manufacturers
            .CreateAsync("Square", "Bangladesh", CancellationToken.None);
        (await duplicateManufacturer.Should().ThrowAsync<ConflictException>())
            .Which.Code.Should().Be("manufacturer.duplicate");
    }

    [Fact]
    public async Task Referenced_categories_and_manufacturers_cannot_be_deleted()
    {
        Guid categoryId;
        Guid manufacturerId;

        await using (var db = _fixture.CreateAppContext(TenantA))
        {
            categoryId = (await new CategoryRepository(db)
                .CreateAsync("Analgesic", null, CancellationToken.None)).Id;
            manufacturerId = (await new ManufacturerRepository(db)
                .CreateAsync("Square", "Bangladesh", CancellationToken.None)).Id;

            await new MedicineRepository(db).CreateAsync(
                Input(barcode: "1", categoryId: categoryId, manufacturerId: manufacturerId),
                CancellationToken.None);
        }

        await using var reader = _fixture.CreateAppContext(TenantA);

        var deleteCategory = async () => await new CategoryRepository(reader)
            .DeleteAsync(categoryId, CancellationToken.None);
        (await deleteCategory.Should().ThrowAsync<ConflictException>())
            .Which.Code.Should().Be("category.in_use");

        var deleteManufacturer = async () => await new ManufacturerRepository(reader)
            .DeleteAsync(manufacturerId, CancellationToken.None);
        (await deleteManufacturer.Should().ThrowAsync<ConflictException>())
            .Which.Code.Should().Be("manufacturer.in_use");
    }

    [Fact]
    public async Task Unreferenced_categories_can_be_deleted()
    {
        Guid categoryId;
        await using (var db = _fixture.CreateAppContext(TenantA))
        {
            categoryId = (await new CategoryRepository(db)
                .CreateAsync("Analgesic", null, CancellationToken.None)).Id;
        }

        await using var reader = _fixture.CreateAppContext(TenantA);
        await new CategoryRepository(reader).DeleteAsync(categoryId, CancellationToken.None);

        (await new CategoryRepository(reader).ListAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task Pharmacy_profile_upsert_round_trip_is_per_tenant()
    {
        await using (var alpha = _fixture.CreateAppContext(TenantA))
        {
            var profiles = new PharmacyProfileStore(alpha);
            (await profiles.GetAsync(CancellationToken.None)).Should().BeNull();

            var saved = await profiles.UpsertAsync(
                Profile("Alpha Pharmacy"), CancellationToken.None);
            saved.PharmacyName.Should().Be("Alpha Pharmacy");
            saved.Currency.Should().Be("BDT");

            (await profiles.GetAsync(CancellationToken.None))!
                .PharmacyName.Should().Be("Alpha Pharmacy");

            // Second upsert updates the same row instead of adding another.
            await profiles.UpsertAsync(Profile("Alpha Pharmacy Ltd"), CancellationToken.None);
            (await profiles.GetAsync(CancellationToken.None))!
                .PharmacyName.Should().Be("Alpha Pharmacy Ltd");
        }

        await using var beta = _fixture.CreateAppContext(TenantB);
        (await new PharmacyProfileStore(beta).GetAsync(CancellationToken.None)).Should().BeNull();
    }

    private static MedicineInput Input(
        string? barcode,
        Guid? categoryId = null,
        Guid? manufacturerId = null) => new(
            BrandName: "Napa",
            GenericName: "Paracetamol",
            Strength: "500mg",
            DosageForm: "Tablet",
            ManufacturerId: manufacturerId,
            CategoryId: categoryId,
            Barcode: barcode,
            Unit: "strip",
            PackSize: 10,
            PurchasePrice: 8.00m,
            SellingPrice: 12.00m,
            ReorderLevel: 50,
            VatRate: null,
            RequiresPrescription: false);

    private static PharmacyProfileDto Profile(string name) => new(
        PharmacyName: name,
        Address: "Dhaka",
        Phone: "+880-2-0000000",
        Email: "info@example.local",
        Website: null,
        RegistrationInfo: null,
        VatInfo: null,
        InvoiceFooter: null,
        Currency: "bdt",
        DefaultLanguage: "EN",
        Timezone: "Asia/Dhaka",
        LogoUrl: null);
}
