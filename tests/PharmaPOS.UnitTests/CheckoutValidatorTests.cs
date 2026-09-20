using FluentAssertions;
using PharmaPOS.Application.Common.Sales;
using PharmaPOS.Application.Features.Sales.Checkout;
using Xunit;

namespace PharmaPOS.UnitTests;

public sealed class CheckoutValidatorTests
{
    private static CheckoutItemInput Item() => new(
        MedicineId: Guid.NewGuid(),
        Quantity: 2,
        BatchId: null,
        DiscountAmount: 0);

    private static CheckoutPaymentInput Cash(decimal amount) => new(
        Method: "Cash",
        Amount: amount,
        Reference: null);

    private static CheckoutInput ValidInput() => new(
        Items: [Item()],
        Payments: [Cash(100.00m)],
        DiscountAmount: 0,
        CustomerId: null,
        CustomerName: null,
        CustomerPhone: null,
        PrescriptionReference: null,
        IdempotencyKey: Guid.NewGuid().ToString());

    [Fact]
    public async Task Accepts_a_well_formed_checkout()
    {
        var result = await new CheckoutInputValidator().ValidateAsync(ValidInput());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Rejects_empty_items_and_payments()
    {
        var validator = new CheckoutInputValidator();

        var noItems = await validator.ValidateAsync(ValidInput() with { Items = [] });
        noItems.IsValid.Should().BeFalse();

        var noPayments = await validator.ValidateAsync(ValidInput() with
        {
            Payments = [],
        });
        noPayments.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Bitcoin")]
    [InlineData("cash")]
    public async Task Rejects_unknown_payment_methods(string method)
    {
        var result = await new CheckoutInputValidator().ValidateAsync(
            ValidInput() with
            {
                Payments = [new CheckoutPaymentInput(method, 10.00m, null)],
            });

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Card")]
    [InlineData("bKash")]
    [InlineData("Nagad")]
    [InlineData("Other")]
    public async Task Requires_a_reference_for_non_cash_payments(string method)
    {
        var result = await new CheckoutInputValidator().ValidateAsync(
            ValidInput() with
            {
                Payments = [new CheckoutPaymentInput(method, 10.00m, null)],
            });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Accepts_non_cash_with_reference_and_rejects_zero_amounts()
    {
        var validator = new CheckoutInputValidator();

        var withReference = await validator.ValidateAsync(
            ValidInput() with
            {
                Payments = [new CheckoutPaymentInput("bKash", 10.00m, "TRX123")],
            });
        withReference.IsValid.Should().BeTrue();

        var zero = await validator.ValidateAsync(
            ValidInput() with
            {
                Payments = [Cash(0)],
            });
        zero.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Requires_an_idempotency_key()
    {
        var result = await new CheckoutCommandValidator()
            .ValidateAsync(new CheckoutCommand(ValidInput() with { IdempotencyKey = string.Empty }));

        result.IsValid.Should().BeFalse();
    }
}
