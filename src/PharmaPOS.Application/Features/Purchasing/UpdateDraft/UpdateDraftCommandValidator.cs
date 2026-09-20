using FluentValidation;
using PharmaPOS.Application.Features.Purchasing.SavePurchase;

namespace PharmaPOS.Application.Features.Purchasing.UpdateDraft;

public sealed class UpdateDraftCommandValidator : AbstractValidator<UpdateDraftCommand>
{
    public UpdateDraftCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Input).NotNull().SetValidator(new PurchaseInputValidator());
    }
}