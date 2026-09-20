using FluentValidation;

namespace PharmaPOS.Application.Features.Tenancy.UpdateRole;

public sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();

        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(command => command.Description)
            .MaximumLength(256);

        RuleFor(command => command.Permissions)
            .NotNull();
    }
}