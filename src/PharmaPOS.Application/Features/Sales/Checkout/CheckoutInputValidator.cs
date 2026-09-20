using FluentValidation;
using PharmaPOS.Application.Common.Sales;
using PharmaPOS.Domain.Sales;

namespace PharmaPOS.Application.Features.Sales.Checkout;

/// <summary>Shape validation for checkout (money math and stock checks live in the service).</summary>
public sealed class CheckoutInputValidator : AbstractValidator<CheckoutInput>
{
    public CheckoutInputValidator()
    {
        RuleFor(input => input.Items).NotNull().NotEmpty();
        RuleFor(input => input.Items.Count).InclusiveBetween(1, 100);
        RuleForEach(input => input.Items).SetValidator(new CheckoutItemValidator());

        RuleFor(input => input.Payments).NotNull().NotEmpty();
        RuleFor(input => input.Payments.Count).InclusiveBetween(1, 4);
        RuleForEach(input => input.Payments).SetValidator(new CheckoutPaymentValidator());

        RuleFor(input => input.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(input => input.CustomerId).NotEqual(Guid.Empty).When(input => input.CustomerId.HasValue);
        RuleFor(input => input.CustomerName).MaximumLength(160);
        RuleFor(input => input.CustomerPhone).MaximumLength(64);
        RuleFor(input => input.PrescriptionReference).MaximumLength(64);
        RuleFor(input => input.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

public sealed class CheckoutItemValidator : AbstractValidator<CheckoutItemInput>
{
    public CheckoutItemValidator()
    {
        RuleFor(item => item.MedicineId).NotEmpty();
        RuleFor(item => item.Quantity).InclusiveBetween(1, 1000);
        RuleFor(item => item.DiscountAmount).GreaterThanOrEqualTo(0);
    }
}

public sealed class CheckoutPaymentValidator : AbstractValidator<CheckoutPaymentInput>
{
    public CheckoutPaymentValidator()
    {
        RuleFor(payment => payment.Method)
            .NotEmpty()
            .Must(PaymentMethods.All.Contains)
            .WithMessage($"Payment method must be one of: {string.Join(", ", PaymentMethods.All)}.");
        RuleFor(payment => payment.Amount).GreaterThan(0);
        RuleFor(payment => payment.Reference)
            .NotEmpty()
            .When(payment => PaymentMethods.RequiresReference(payment.Method))
            .WithMessage("A transaction reference is required for non-cash payments.");
        RuleFor(payment => payment.Reference).MaximumLength(64);
    }
}
