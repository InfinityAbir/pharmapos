using FluentValidation;
using PharmaPOS.Application.Features.Purchasing.SaveSupplier;

namespace PharmaPOS.Application.Features.Purchasing.UpdateSupplier;

public sealed class UpdateSupplierCommandValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Input).NotNull().SetValidator(new SupplierInputValidator());
    }
}