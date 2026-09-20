using FluentValidation;
using PharmaPOS.Application.Features.Catalog.SaveMedicine;

namespace PharmaPOS.Application.Features.Catalog.CreateMedicine;

public sealed class CreateMedicineCommandValidator : AbstractValidator<CreateMedicineCommand>
{
    public CreateMedicineCommandValidator()
    {
        RuleFor(command => command.Input).NotNull().SetValidator(new MedicineInputValidator());
    }
}