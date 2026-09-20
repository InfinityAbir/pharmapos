using FluentValidation;
using PharmaPOS.Application.Features.Customers.SaveCustomer;

namespace PharmaPOS.Application.Features.Customers.CreateCustomer;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(command => command.Input).NotNull().SetValidator(new CustomerInputValidator());
    }
}
