using FluentAssertions;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Catalog;
using PharmaPOS.Application.Features.Catalog;
using PharmaPOS.Application.Features.Catalog.UpdateMedicine;
using Xunit;

namespace PharmaPOS.UnitTests;

public sealed class UpdateMedicineHandlerTests
{
    [Fact]
    public async Task Emits_price_changed_event_only_when_a_price_differs()
    {
        var before = Dto(purchase: 8.00m, selling: 12.00m);
        var after = Dto(purchase: 9.00m, selling: 12.00m);
        var medicines = new StubMedicines(before, after);
        var audit = new CapturingAuditLogger();

        await new UpdateMedicineCommandHandler(medicines, audit).Handle(
            new UpdateMedicineCommand(before.Id, TestInput()),
            CancellationToken.None);

        audit.Entries.Select(entry => entry.Action)
            .Should().BeEquivalentTo("medicine.updated", "medicine.price.changed");
    }

    [Fact]
    public async Task Skips_price_changed_event_when_prices_are_unchanged()
    {
        var medicine = Dto(purchase: 8.00m, selling: 12.00m);
        var medicines = new StubMedicines(medicine, medicine);
        var audit = new CapturingAuditLogger();

        await new UpdateMedicineCommandHandler(medicines, audit).Handle(
            new UpdateMedicineCommand(medicine.Id, TestInput()),
            CancellationToken.None);

        audit.Entries.Should().ContainSingle()
            .Which.Action.Should().Be("medicine.updated");
    }

    private static MedicineDto Dto(decimal purchase, decimal selling) => new(
        Id: Guid.NewGuid(),
        BrandName: "Napa",
        GenericName: "Paracetamol",
        Strength: "500mg",
        DosageForm: "Tablet",
        ManufacturerId: null,
        ManufacturerName: null,
        CategoryId: null,
        CategoryName: null,
        Barcode: null,
        Unit: "strip",
        PackSize: 10,
        PurchasePrice: purchase,
        SellingPrice: selling,
        ReorderLevel: 50,
        VatRate: null,
        RequiresPrescription: false,
        IsActive: true);

    private static MedicineInput TestInput() => new(
        BrandName: "Napa",
        GenericName: "Paracetamol",
        Strength: "500mg",
        DosageForm: "Tablet",
        ManufacturerId: null,
        CategoryId: null,
        Barcode: null,
        Unit: "strip",
        PackSize: 10,
        PurchasePrice: 9.00m,
        SellingPrice: 12.00m,
        ReorderLevel: 50,
        VatRate: null,
        RequiresPrescription: false);

    private sealed class StubMedicines(MedicineDto before, MedicineDto after) : IMedicineRepository
    {
        public Task<MedicineDto?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<MedicineDto?>(before);

        public Task<MedicineDto> UpdateAsync(
            Guid id,
            MedicineInput input,
            CancellationToken cancellationToken = default)
            => Task.FromResult(after);

        public Task<IReadOnlyList<MedicineDto>> SearchAsync(
            string? search, Guid? categoryId, Guid? manufacturerId, bool? isActive,
            int skip, int take, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MedicineDto?> FindByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MedicineDto> CreateAsync(MedicineInput input, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MedicineDto> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> CountAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> CountActiveAsync(CancellationToken cancellationToken = default)
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
