using FluentValidation;
using PharmaPOS.Application.Features.Customers.SaveCustomer;

namespace PharmaPOS.Application.Features.Customers.UpdateCustomer;

public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Input).NotNull().SetValidator(new CustomerInputValidator());
    }
}
