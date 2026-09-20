using FluentValidation;
using PharmaPOS.Application.Features.Catalog.SaveMedicine;

namespace PharmaPOS.Application.Features.Catalog.UpdateMedicine;

public sealed class UpdateMedicineCommandValidator : AbstractValidator<UpdateMedicineCommand>
{
    public UpdateMedicineCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Input).NotNull().SetValidator(new MedicineInputValidator());
    }
}