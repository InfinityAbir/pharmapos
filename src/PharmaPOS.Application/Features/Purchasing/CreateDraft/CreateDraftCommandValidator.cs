using FluentValidation;
using PharmaPOS.Application.Features.Purchasing.SavePurchase;

namespace PharmaPOS.Application.Features.Purchasing.CreateDraft;

public sealed class CreateDraftCommandValidator : AbstractValidator<CreateDraftCommand>
{
    public CreateDraftCommandValidator()
    {
        RuleFor(command => command.Input).NotNull().SetValidator(new PurchaseInputValidator());
    }
}