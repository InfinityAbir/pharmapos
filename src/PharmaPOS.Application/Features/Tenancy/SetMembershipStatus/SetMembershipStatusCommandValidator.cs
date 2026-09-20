using FluentValidation;

namespace PharmaPOS.Application.Features.Tenancy.SetMembershipStatus;

public sealed class SetMembershipStatusCommandValidator : AbstractValidator<SetMembershipStatusCommand>
{
    public SetMembershipStatusCommandValidator()
    {
        RuleFor(command => command.MembershipId).NotEmpty();

        RuleFor(command => command.Status)
            .Must(status => status is "Active" or "Inactive")
            .WithMessage("Status must be 'Active' or 'Inactive'.");
    }
}