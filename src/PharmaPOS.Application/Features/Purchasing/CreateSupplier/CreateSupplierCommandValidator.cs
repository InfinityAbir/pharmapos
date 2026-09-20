using FluentValidation;
using PharmaPOS.Application.Features.Purchasing.SaveSupplier;

namespace PharmaPOS.Application.Features.Purchasing.CreateSupplier;

public sealed class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        RuleFor(command => command.Input).NotNull().SetValidator(new SupplierInputValidator());
    }
}