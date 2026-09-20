using FluentValidation;
using PharmaPOS.Application.Common.Customers;

namespace PharmaPOS.Application.Features.Customers.SaveCustomer;

/// <summary>Shared shape validation for customer create/update.</summary>
public sealed class CustomerInputValidator : AbstractValidator<CustomerInput>
{
    public CustomerInputValidator()
    {
        RuleFor(input => input.Name).NotEmpty().MaximumLength(160);
        RuleFor(input => input.Phone).MaximumLength(64);
        RuleFor(input => input.Email)
            .EmailAddress()
            .When(input => !string.IsNullOrWhiteSpace(input.Email));
        RuleFor(input => input.Address).MaximumLength(512);
        RuleFor(input => input.Notes).MaximumLength(1024);
    }
}
