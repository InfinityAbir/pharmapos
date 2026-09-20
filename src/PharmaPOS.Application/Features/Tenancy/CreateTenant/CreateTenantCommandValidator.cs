using FluentValidation;

namespace PharmaPOS.Application.Features.Tenancy.CreateTenant;

public sealed class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(command => command.Identifier)
            .NotEmpty()
            .MaximumLength(64)
            .Matches("^[a-z0-9][a-z0-9-]{0,62}[a-z0-9]$")
            .WithMessage("Identifier must be 2-64 characters: lowercase letters, digits and dashes, not leading or trailing with a dash.");

        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(160);
    }
}