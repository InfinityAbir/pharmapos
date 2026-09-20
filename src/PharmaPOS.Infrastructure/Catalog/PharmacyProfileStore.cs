using Microsoft.EntityFrameworkCore;
using PharmaPOS.Application.Common.Catalog;
using PharmaPOS.Application.Features.Catalog;
using PharmaPOS.Domain.Catalog;
using PharmaPOS.Infrastructure.Persistence;

namespace PharmaPOS.Infrastructure.Catalog;

/// <summary>Tenant-scoped pharmacy profile with upsert semantics (one row per tenant).</summary>
public sealed class PharmacyProfileStore : IPharmacyProfileStore
{
    private readonly AppDbContext _dbContext;

    public PharmacyProfileStore(AppDbContext dbContext)
        => _dbContext = dbContext;

    public async Task<PharmacyProfileDto?> GetAsync(CancellationToken cancellationToken)
    {
        var profile = await _dbContext.PharmacyProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return profile is null ? null : ToDto(profile);
    }

    public async Task<PharmacyProfileDto> UpsertAsync(
        PharmacyProfileDto profile,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.PharmacyProfiles
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            existing = new PharmacyProfile
            {
                TenantId = TenantScope.CurrentTenantId(_dbContext),
            };

            _dbContext.PharmacyProfiles.Add(existing);
        }

        existing.PharmacyName = profile.PharmacyName.Trim();
        existing.Address = Normalize(profile.Address);
        existing.Phone = Normalize(profile.Phone);
        existing.Email = Normalize(profile.Email)?.ToLowerInvariant();
        existing.Website = Normalize(profile.Website);
        existing.RegistrationInfo = Normalize(profile.RegistrationInfo);
        existing.VatInfo = Normalize(profile.VatInfo);
        existing.InvoiceFooter = Normalize(profile.InvoiceFooter);
        existing.Currency = profile.Currency.Trim().ToUpperInvariant();
        existing.DefaultLanguage = profile.DefaultLanguage.Trim().ToLowerInvariant();
        existing.Timezone = profile.Timezone.Trim();
        existing.LogoUrl = Normalize(profile.LogoUrl);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(existing);
    }

    private static PharmacyProfileDto ToDto(PharmacyProfile profile)
        => new(
            PharmacyName: profile.PharmacyName,
            Address: profile.Address,
            Phone: profile.Phone,
            Email: profile.Email,
            Website: profile.Website,
            RegistrationInfo: profile.RegistrationInfo,
            VatInfo: profile.VatInfo,
            InvoiceFooter: profile.InvoiceFooter,
            Currency: profile.Currency,
            DefaultLanguage: profile.DefaultLanguage,
            Timezone: profile.Timezone,
            LogoUrl: profile.LogoUrl);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
