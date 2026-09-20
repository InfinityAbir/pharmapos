using FluentValidation;

namespace PharmaPOS.Application.Features.Tenancy.SetTenantStatus;

public sealed class SetTenantStatusCommandValidator : AbstractValidator<SetTenantStatusCommand>
{
    public SetTenantStatusCommandValidator()
    {
        RuleFor(command => command.Identifier).NotEmpty().MaximumLength(64);

        RuleFor(command => command.Status)
            .Must(status => status is TenantStatus.Active or TenantStatus.Suspended)
            .WithMessage($"Status must be '{TenantStatus.Active}' or '{TenantStatus.Suspended}'.");
    }
}