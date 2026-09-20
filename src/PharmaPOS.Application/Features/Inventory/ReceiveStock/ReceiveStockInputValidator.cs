using FluentValidation;
using PharmaPOS.Application.Common.Inventory;

namespace PharmaPOS.Application.Features.Inventory.ReceiveStock;

/// <summary>Shape validation for stock receipt (existence checks live in the repository).</summary>
public sealed class ReceiveStockInputValidator : AbstractValidator<ReceiveStockInput>
{
    public ReceiveStockInputValidator()
    {
        RuleFor(input => input.MedicineId).NotEmpty();
        RuleFor(input => input.BatchNumber).NotEmpty().MaximumLength(64);
        RuleFor(input => input.Quantity).InclusiveBetween(1, 1_000_000);
        RuleFor(input => input.PurchasePrice).GreaterThanOrEqualTo(0).When(input => input.PurchasePrice.HasValue);
        RuleFor(input => input.SellingPrice).GreaterThanOrEqualTo(0).When(input => input.SellingPrice.HasValue);
        RuleFor(input => input.ExpiryDate)
            .GreaterThan(DateOnly.FromDateTime(DateTime.UtcNow.Date))
            .WithMessage("Expiry date must be in the future. Expired stock can never be received.");
        RuleFor(input => input.ManufactureDate)
            .LessThanOrEqualTo(input => input.ExpiryDate)
            .When(input => input.ManufactureDate.HasValue)
            .WithMessage("Manufacture date cannot be after the expiry date.");
        RuleFor(input => input.SupplierName).MaximumLength(160);
        RuleFor(input => input.PurchaseReference).MaximumLength(64);
    }
}
