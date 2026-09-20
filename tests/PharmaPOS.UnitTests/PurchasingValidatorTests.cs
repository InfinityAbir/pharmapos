using FluentAssertions;
using PharmaPOS.Application.Common.Purchasing;
using PharmaPOS.Application.Features.Purchasing.CreateDraft;
using PharmaPOS.Application.Features.Purchasing.CreateSupplier;
using PharmaPOS.Application.Features.Purchasing.ReceivePurchase;
using PharmaPOS.Domain.Exceptions;
using Xunit;

namespace PharmaPOS.UnitTests;

public sealed class PurchasingValidatorTests
{
    private static PurchaseItemInput Item(
        int quantity = 10,
        decimal unitPrice = 8.00m,
        decimal discount = 0) => new(
            MedicineId: Guid.NewGuid(),
            BatchNumber: "NP-001",
            ManufactureDate: null,
            ExpiryDate: DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(300)),
            Quantity: quantity,
            UnitPrice: unitPrice,
            DiscountAmount: discount,
            SellingPrice: null);

    private static PurchaseInput ValidInput() => new(
        SupplierId: Guid.NewGuid(),
        PurchaseDate: DateOnly.FromDateTime(DateTime.UtcNow.Date),
        SupplierInvoiceNumber: "SQ-001",
        DiscountAmount: 5.00m,
        TaxAmount: 2.00m,
        Notes: null,
        Items: [Item()]);

    [Fact]
    public void Totals_math_is_subtotal_minus_discount_plus_tax()
    {
        var (subtotal, total) = PurchaseTotals.Calculate(
            [Item(quantity: 10, unitPrice: 8.00m, discount: 5.00m)],
            headerDiscount: 5.00m,
            tax: 2.00m);

        subtotal.Should().Be(75.00m);
        total.Should().Be(72.00m);
    }

    [Fact]
    public void Item_discount_beyond_line_gross_is_rejected()
    {
        var act = () => PurchaseTotals.Calculate(
            [Item(quantity: 1, unitPrice: 8.00m, discount: 9.00m)],
            headerDiscount: 0,
            tax: 0);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("purchase.item_discount_exceeded");
    }

    [Fact]
    public void Header_discount_beyond_subtotal_is_rejected()
    {
        var act = () => PurchaseTotals.Calculate(
            [Item(quantity: 1, unitPrice: 8.00m)],
            headerDiscount: 9.00m,
            tax: 0);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("purchase.discount_exceeded");
    }

    [Fact]
    public async Task Draft_rejects_empty_items_and_future_dates()
    {
        var validator = new CreateDraftCommandValidator();

        var empty = await validator.ValidateAsync(
            new CreateDraftCommand(ValidInput() with { Items = [] }));
        empty.IsValid.Should().BeFalse();

        var future = await validator.ValidateAsync(
            new CreateDraftCommand(ValidInput() with
            {
                PurchaseDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(5)),
            }));
        future.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Draft_rejects_past_expiry()
    {
        var input = ValidInput() with
        {
            Items = [Item() with
            {
                ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1)),
            }],
        };

        var result = await new CreateDraftCommandValidator()
            .ValidateAsync(new CreateDraftCommand(input));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Receive_requires_an_idempotency_key()
    {
        var result = await new ReceivePurchaseCommandValidator()
            .ValidateAsync(new ReceivePurchaseCommand(Guid.NewGuid(), string.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Supplier_rejects_empty_name_and_bad_email()
    {
        var validator = new CreateSupplierCommandValidator();

        var empty = await validator.ValidateAsync(new CreateSupplierCommand(
            new SupplierInput(string.Empty, null, null, null, null, null, null)));
        empty.IsValid.Should().BeFalse();

        var badEmail = await validator.ValidateAsync(new CreateSupplierCommand(
            new SupplierInput("Square", null, null, "not-an-email", null, null, null)));
        badEmail.IsValid.Should().BeFalse();
    }
}
