using FluentValidation;
using PharmaPOS.Application.Common.Purchasing;

namespace PharmaPOS.Application.Features.Purchasing.SaveSupplier;

/// <summary>Shared shape validation for supplier create/update.</summary>
public sealed class SupplierInputValidator : AbstractValidator<SupplierInput>
{
    public SupplierInputValidator()
    {
        RuleFor(input => input.Name).NotEmpty().MaximumLength(128);
        RuleFor(input => input.Company).MaximumLength(160);
        RuleFor(input => input.Phone).MaximumLength(64);
        RuleFor(input => input.Email)
            .EmailAddress()
            .When(input => !string.IsNullOrWhiteSpace(input.Email));
        RuleFor(input => input.Address).MaximumLength(512);
        RuleFor(input => input.ContactPerson).MaximumLength(160);
        RuleFor(input => input.PaymentTerms).MaximumLength(256);
    }
}
