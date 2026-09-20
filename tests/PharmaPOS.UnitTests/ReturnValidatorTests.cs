using FluentAssertions;
using PharmaPOS.Application.Common.Sales;
using PharmaPOS.Application.Features.Returns.ProcessReturn;
using Xunit;

namespace PharmaPOS.UnitTests;

public sealed class ReturnValidatorTests
{
    private static ReturnInput Valid() => new(
        SaleId: Guid.NewGuid(),
        Items: [new ReturnItemInput(Guid.NewGuid(), 1)],
        Reason: "Wrong item",
        Restock: true,
        RefundMethod: "Cash",
        RefundReference: null,
        IdempotencyKey: Guid.NewGuid().ToString());

    [Fact]
    public async Task Accepts_a_well_formed_return()
    {
        var result = await new ReturnInputValidator().ValidateAsync(Valid());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Rejects_empty_items_and_reason()
    {
        var validator = new ReturnInputValidator();

        var noItems = await validator.ValidateAsync(Valid() with { Items = [] });
        noItems.IsValid.Should().BeFalse();

        var noReason = await validator.ValidateAsync(Valid() with { Reason = "" });
        noReason.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Card")]
    [InlineData("bKash")]
    [InlineData("Nagad")]
    [InlineData("Other")]
    public async Task Requires_a_reference_for_non_cash_refunds(string method)
    {
        var result = await new ReturnInputValidator().ValidateAsync(Valid() with { RefundMethod = method });
        result.IsValid.Should().BeFalse();

        var withRef = await new ReturnInputValidator().ValidateAsync(
            Valid() with { RefundMethod = method, RefundReference = "TRX123" });
        withRef.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Rejects_zero_quantities_and_missing_key()
    {
        var zero = await new ReturnInputValidator().ValidateAsync(
            Valid() with { Items = [new ReturnItemInput(Guid.NewGuid(), 0)] });
        zero.IsValid.Should().BeFalse();

        var noKey = await new ProcessReturnCommandValidator().ValidateAsync(
            new ProcessReturnCommand(Valid() with { IdempotencyKey = "" }));
        noKey.IsValid.Should().BeFalse();
    }
}
