using FluentValidation;

namespace PharmaPOS.Application.Features.Purchasing.ReceivePurchase;

public sealed class ReceivePurchaseCommandValidator : AbstractValidator<ReceivePurchaseCommand>
{
    public ReceivePurchaseCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}