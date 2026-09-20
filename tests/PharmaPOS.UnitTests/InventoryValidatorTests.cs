using FluentAssertions;
using PharmaPOS.Application.Common.Inventory;
using PharmaPOS.Application.Features.Inventory.AdjustStock;
using PharmaPOS.Application.Features.Inventory.ReceiveStock;
using Xunit;

namespace PharmaPOS.UnitTests;

public sealed class InventoryValidatorTests
{
    private static ReceiveStockInput ValidInput() => new(
        MedicineId: Guid.NewGuid(),
        BatchNumber: "NP-001",
        Quantity: 100,
        PurchasePrice: 8.00m,
        SellingPrice: 12.00m,
        ManufactureDate: DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-30)),
        ExpiryDate: DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(300)),
        SupplierName: "Square Distributor",
        PurchaseReference: "PO-2026-001");

    [Fact]
    public async Task Receive_accepts_a_well_formed_input()
    {
        var result = await new ReceiveStockInputValidator().ValidateAsync(ValidInput());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Receive_rejects_a_past_expiry_date()
    {
        var result = await new ReceiveStockInputValidator().ValidateAsync(
            ValidInput() with
            {
                ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1)),
            });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Receive_rejects_manufacture_after_expiry()
    {
        var result = await new ReceiveStockInputValidator().ValidateAsync(
            ValidInput() with
            {
                ManufactureDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(400)),
            });

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Receive_rejects_non_positive_quantities(int quantity)
    {
        var result = await new ReceiveStockInputValidator().ValidateAsync(
            ValidInput() with { Quantity = quantity });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Receive_command_validates_the_nested_input()
    {
        var result = await new ReceiveStockCommandValidator()
            .ValidateAsync(new ReceiveStockCommand(ValidInput() with { BatchNumber = string.Empty }));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Adjust_rejects_a_zero_change()
    {
        var result = await new AdjustStockCommandValidator()
            .ValidateAsync(new AdjustStockCommand(Guid.NewGuid(), 0, "no-op"));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(5)]
    [InlineData(-3)]
    public async Task Adjust_accepts_signed_changes(int change)
    {
        var result = await new AdjustStockCommandValidator()
            .ValidateAsync(new AdjustStockCommand(Guid.NewGuid(), change, "count correction"));

        result.IsValid.Should().BeTrue();
    }
}
