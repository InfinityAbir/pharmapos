using FluentValidation;

namespace PharmaPOS.Application.Features.Catalog.UpdateManufacturer;

public sealed class UpdateManufacturerCommandValidator : AbstractValidator<UpdateManufacturerCommand>
{
    public UpdateManufacturerCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(128);
        RuleFor(command => command.Country).MaximumLength(64);
    }
}