using FluentValidation;

namespace PharmaPOS.Application.Features.Catalog.UpsertPharmacyProfile;

public sealed class UpsertPharmacyProfileCommandValidator : AbstractValidator<UpsertPharmacyProfileCommand>
{
    public UpsertPharmacyProfileCommandValidator()
    {
        RuleFor(command => command.Profile).NotNull();

        RuleFor(command => command.Profile.PharmacyName).NotEmpty().MaximumLength(160);
        RuleFor(command => command.Profile.Address).MaximumLength(512);
        RuleFor(command => command.Profile.Phone).MaximumLength(64);
        RuleFor(command => command.Profile.Email)
            .EmailAddress()
            .When(command => !string.IsNullOrWhiteSpace(command.Profile.Email));
        RuleFor(command => command.Profile.Website).MaximumLength(256);
        RuleFor(command => command.Profile.RegistrationInfo).MaximumLength(256);
        RuleFor(command => command.Profile.VatInfo).MaximumLength(256);
        RuleFor(command => command.Profile.InvoiceFooter).MaximumLength(512);
        RuleFor(command => command.Profile.Currency).NotEmpty().MaximumLength(8);
        RuleFor(command => command.Profile.DefaultLanguage).NotEmpty().MaximumLength(8);
        RuleFor(command => command.Profile.Timezone).NotEmpty().MaximumLength(64);
        RuleFor(command => command.Profile.LogoUrl).MaximumLength(512);
    }
}