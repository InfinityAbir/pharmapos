using FluentValidation;

namespace PharmaPOS.Application.Features.Inventory.ReceiveStock;

public sealed class ReceiveStockCommandValidator : AbstractValidator<ReceiveStockCommand>
{
    public ReceiveStockCommandValidator()
    {
        RuleFor(command => command.Input).NotNull().SetValidator(new ReceiveStockInputValidator());
    }
}