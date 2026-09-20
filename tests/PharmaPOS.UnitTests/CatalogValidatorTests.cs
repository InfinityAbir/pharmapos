using FluentAssertions;
using PharmaPOS.Application.Common.Catalog;
using PharmaPOS.Application.Features.Catalog.CreateCategory;
using PharmaPOS.Application.Features.Catalog.CreateManufacturer;
using PharmaPOS.Application.Features.Catalog.CreateMedicine;
using PharmaPOS.Application.Features.Catalog.SaveMedicine;
using PharmaPOS.Application.Features.Catalog.UpsertPharmacyProfile;
using PharmaPOS.Domain.Catalog;
using Xunit;

namespace PharmaPOS.UnitTests;

public sealed class CatalogValidatorTests
{
    private static MedicineInput ValidInput() => new(
        BrandName: "Napa",
        GenericName: "Paracetamol",
        Strength: "500mg",
        DosageForm: DosageForms.Tablet,
        ManufacturerId: null,
        CategoryId: null,
        Barcode: "8941154100017",
        Unit: "strip",
        PackSize: 10,
        PurchasePrice: 8.00m,
        SellingPrice: 12.00m,
        ReorderLevel: 50,
        VatRate: null,
        RequiresPrescription: false);

    [Fact]
    public async Task Medicine_accepts_a_well_formed_input()
    {
        var result = await new MedicineInputValidator().ValidateAsync(ValidInput());

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("SuppositoryX")]
    [InlineData("tablet")]
    public async Task Medicine_rejects_unknown_dosage_forms(string form)
    {
        var result = await new MedicineInputValidator()
            .ValidateAsync(ValidInput() with { DosageForm = form });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Medicine_accepts_every_known_dosage_form()
    {
        foreach (var form in DosageForms.All)
        {
            var result = await new MedicineInputValidator()
                .ValidateAsync(ValidInput() with { DosageForm = form });

            result.IsValid.Should().BeTrue(because: $"form '{form}' is in the catalog list");
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public async Task Medicine_rejects_negative_prices(decimal price)
    {
        var result = await new MedicineInputValidator()
            .ValidateAsync(ValidInput() with { SellingPrice = price });

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Medicine_rejects_out_of_range_vat(decimal vat)
    {
        var result = await new MedicineInputValidator()
            .ValidateAsync(ValidInput() with { VatRate = vat });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Medicine_rejects_non_positive_pack_size()
    {
        var result = await new MedicineInputValidator()
            .ValidateAsync(ValidInput() with { PackSize = 0 });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateMedicine_validates_the_nested_input()
    {
        var result = await new CreateMedicineCommandValidator()
            .ValidateAsync(new CreateMedicineCommand(ValidInput() with { BrandName = string.Empty }));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateCategory_rejects_an_empty_name()
    {
        var result = await new CreateCategoryCommandValidator()
            .ValidateAsync(new CreateCategoryCommand(string.Empty, null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateManufacturer_rejects_an_empty_name()
    {
        var result = await new CreateManufacturerCommandValidator()
            .ValidateAsync(new CreateManufacturerCommand(string.Empty, null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UpsertProfile_rejects_missing_name_and_bad_email()
    {
        var validator = new UpsertPharmacyProfileCommandValidator();

        var missingName = await validator.ValidateAsync(new UpsertPharmacyProfileCommand(
            new Application.Features.Catalog.PharmacyProfileDto(
                PharmacyName: string.Empty, Address: null, Phone: null, Email: null,
                Website: null, RegistrationInfo: null, VatInfo: null, InvoiceFooter: null,
                Currency: "BDT", DefaultLanguage: "en", Timezone: "Asia/Dhaka", LogoUrl: null)));

        missingName.IsValid.Should().BeFalse();

        var badEmail = await validator.ValidateAsync(new UpsertPharmacyProfileCommand(
            new Application.Features.Catalog.PharmacyProfileDto(
                PharmacyName: "Demo", Address: null, Phone: null, Email: "not-an-email",
                Website: null, RegistrationInfo: null, VatInfo: null, InvoiceFooter: null,
                Currency: "BDT", DefaultLanguage: "en", Timezone: "Asia/Dhaka", LogoUrl: null)));

        badEmail.IsValid.Should().BeFalse();
    }
}
