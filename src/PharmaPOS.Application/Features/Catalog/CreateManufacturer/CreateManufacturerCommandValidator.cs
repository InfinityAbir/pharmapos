using FluentValidation;

namespace PharmaPOS.Application.Features.Catalog.CreateManufacturer;

public sealed class CreateManufacturerCommandValidator : AbstractValidator<CreateManufacturerCommand>
{
    public CreateManufacturerCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(128);
        RuleFor(command => command.Country).MaximumLength(64);
    }
}