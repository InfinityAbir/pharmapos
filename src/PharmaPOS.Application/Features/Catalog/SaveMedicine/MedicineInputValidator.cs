using FluentValidation;
using PharmaPOS.Application.Common.Catalog;
using PharmaPOS.Domain.Catalog;

namespace PharmaPOS.Application.Features.Catalog.SaveMedicine;

/// <summary>Shared shape validation for medicine create/update (existence checks live in the repository).</summary>
public sealed class MedicineInputValidator : AbstractValidator<MedicineInput>
{
    public MedicineInputValidator()
    {
        RuleFor(input => input.BrandName).NotEmpty().MaximumLength(160);
        RuleFor(input => input.GenericName).NotEmpty().MaximumLength(160);
        RuleFor(input => input.Strength).NotEmpty().MaximumLength(64);
        RuleFor(input => input.DosageForm)
            .NotEmpty()
            .Must(form => DosageForms.All.Contains(form))
            .WithMessage($"Dosage form must be one of: {string.Join(", ", DosageForms.All)}.");
        RuleFor(input => input.Barcode).MaximumLength(64);
        RuleFor(input => input.Unit).NotEmpty().MaximumLength(32);
        RuleFor(input => input.PackSize).GreaterThan(0).When(input => input.PackSize.HasValue);
        RuleFor(input => input.PurchasePrice).GreaterThanOrEqualTo(0);
        RuleFor(input => input.SellingPrice).GreaterThanOrEqualTo(0);
        RuleFor(input => input.ReorderLevel).GreaterThanOrEqualTo(0);
        RuleFor(input => input.VatRate).InclusiveBetween(0, 100).When(input => input.VatRate.HasValue);
    }
}
