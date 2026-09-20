using FluentValidation;

namespace PharmaPOS.Application.Features.Tenancy.CreateRole;

public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(command => command.Description)
            .MaximumLength(256);

        RuleFor(command => command.Permissions)
            .NotNull();
    }
}