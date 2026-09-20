using FluentValidation;

namespace PharmaPOS.Application.Features.Tenancy.DeleteRole;

public sealed class DeleteRoleCommandValidator : AbstractValidator<DeleteRoleCommand>
{
    public DeleteRoleCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
    }
}