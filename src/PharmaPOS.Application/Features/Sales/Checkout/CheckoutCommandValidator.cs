using FluentValidation;

namespace PharmaPOS.Application.Features.Sales.Checkout;

public sealed class CheckoutCommandValidator : AbstractValidator<CheckoutCommand>
{
    public CheckoutCommandValidator()
    {
        RuleFor(command => command.Input).NotNull().SetValidator(new CheckoutInputValidator());
    }
}