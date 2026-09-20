using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Catalog;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class PharmacyProfileConfiguration : IEntityTypeConfiguration<PharmacyProfile>
{
    public void Configure(EntityTypeBuilder<PharmacyProfile> builder)
    {
        builder.ToTable("pharmacy_profiles");

        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(profile => profile.PharmacyName).HasMaxLength(160).IsRequired();
        builder.Property(profile => profile.Address).HasMaxLength(512);
        builder.Property(profile => profile.Phone).HasMaxLength(64);
        builder.Property(profile => profile.Email).HasMaxLength(320);
        builder.Property(profile => profile.Website).HasMaxLength(256);
        builder.Property(profile => profile.RegistrationInfo).HasMaxLength(256);
        builder.Property(profile => profile.VatInfo).HasMaxLength(256);
        builder.Property(profile => profile.InvoiceFooter).HasMaxLength(512);
        builder.Property(profile => profile.Currency).HasMaxLength(8).IsRequired();
        builder.Property(profile => profile.DefaultLanguage).HasMaxLength(8).IsRequired();
        builder.Property(profile => profile.Timezone).HasMaxLength(64).IsRequired();
        builder.Property(profile => profile.LogoUrl).HasMaxLength(512);

        // Exactly one profile row per tenant.
        builder.HasIndex(profile => profile.TenantId).IsUnique();
    }
}