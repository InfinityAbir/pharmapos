using FluentValidation;
using PharmaPOS.Application.Common.Sales;
using PharmaPOS.Domain.Sales;

namespace PharmaPOS.Application.Features.Returns.ProcessReturn;

/// <summary>Shape validation for returns (returnable quantities and money caps live in the service).</summary>
public sealed class ProcessReturnCommandValidator : AbstractValidator<ProcessReturnCommand>
{
    public ProcessReturnCommandValidator()
    {
        RuleFor(command => command.Input).NotNull().SetValidator(new ReturnInputValidator());
    }
}

public sealed class ReturnInputValidator : AbstractValidator<ReturnInput>
{
    public ReturnInputValidator()
    {
        RuleFor(input => input.SaleId).NotEmpty();
        RuleFor(input => input.Items).NotNull().NotEmpty();
        RuleFor(input => input.Items.Count).InclusiveBetween(1, 100);
        RuleForEach(input => input.Items).SetValidator(new ReturnItemInputValidator());
        RuleFor(input => input.Reason).NotEmpty().MaximumLength(512);
        RuleFor(input => input.RefundMethod)
            .NotEmpty()
            .Must(PaymentMethods.All.Contains)
            .WithMessage($"Refund method must be one of: {string.Join(", ", PaymentMethods.All)}.");
        RuleFor(input => input.RefundReference)
            .NotEmpty()
            .When(input => PaymentMethods.RequiresReference(input.RefundMethod))
            .WithMessage("A transaction reference is required for non-cash refunds.");
        RuleFor(input => input.RefundReference).MaximumLength(64);
        RuleFor(input => input.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

public sealed class ReturnItemInputValidator : AbstractValidator<ReturnItemInput>
{
    public ReturnItemInputValidator()
    {
        RuleFor(item => item.SaleItemId).NotEmpty();
        RuleFor(item => item.Quantity).InclusiveBetween(1, 1000);
    }
}
