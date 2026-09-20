using FluentValidation;

namespace PharmaPOS.Application.Features.Inventory.AdjustStock;

public sealed class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.QuantityChange)
            .NotEqual(0)
            .InclusiveBetween(-1_000_000, 1_000_000);
        RuleFor(command => command.Reason).MaximumLength(256);
    }
}