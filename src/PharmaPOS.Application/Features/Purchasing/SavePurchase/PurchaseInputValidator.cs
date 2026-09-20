using FluentValidation;
using PharmaPOS.Application.Common.Purchasing;

namespace PharmaPOS.Application.Features.Purchasing.SavePurchase;

/// <summary>Shared shape validation for purchase drafts (totals and existence live deeper).</summary>
public sealed class PurchaseInputValidator : AbstractValidator<PurchaseInput>
{
    public PurchaseInputValidator()
    {
        RuleFor(input => input.SupplierId).NotEmpty();
        RuleFor(input => input.PurchaseDate)
            .NotEmpty()
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1)))
            .WithMessage("Purchase date cannot be in the future.");
        RuleFor(input => input.SupplierInvoiceNumber).MaximumLength(64);
        RuleFor(input => input.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(input => input.TaxAmount).GreaterThanOrEqualTo(0);
        RuleFor(input => input.Notes).MaximumLength(512);
        RuleFor(input => input.Items).NotNull().NotEmpty();
        RuleFor(input => input.Items.Count).InclusiveBetween(1, 200);
        RuleForEach(input => input.Items).SetValidator(new PurchaseItemInputValidator());
    }
}

public sealed class PurchaseItemInputValidator : AbstractValidator<PurchaseItemInput>
{
    public PurchaseItemInputValidator()
    {
        RuleFor(item => item.MedicineId).NotEmpty();
        RuleFor(item => item.BatchNumber).NotEmpty().MaximumLength(64);
        RuleFor(item => item.ManufactureDate)
            .LessThanOrEqualTo(item => item.ExpiryDate)
            .When(item => item.ManufactureDate.HasValue)
            .WithMessage("Manufacture date cannot be after the expiry date.");
        RuleFor(item => item.ExpiryDate)
            .GreaterThan(DateOnly.FromDateTime(DateTime.UtcNow.Date))
            .WithMessage("Expiry date must be in the future. Expired stock can never be received.");
        RuleFor(item => item.Quantity).InclusiveBetween(1, 1_000_000);
        RuleFor(item => item.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(item => item.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(item => item.SellingPrice).GreaterThanOrEqualTo(0).When(item => item.SellingPrice.HasValue);
    }
}
