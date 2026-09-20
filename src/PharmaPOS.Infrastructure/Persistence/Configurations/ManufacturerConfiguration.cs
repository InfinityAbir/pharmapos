using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Catalog;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class ManufacturerConfiguration : IEntityTypeConfiguration<Manufacturer>
{
    public void Configure(EntityTypeBuilder<Manufacturer> builder)
    {
        builder.ToTable("manufacturers");

        builder.HasKey(manufacturer => manufacturer.Id);

        builder.Property(manufacturer => manufacturer.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(manufacturer => manufacturer.Name).HasMaxLength(128).IsRequired();
        builder.Property(manufacturer => manufacturer.Country).HasMaxLength(64);

        builder.HasIndex(manufacturer => new { manufacturer.TenantId, manufacturer.Name }).IsUnique();

        // Composite alternate key used by the tenant-composite FK from medicines.
        builder.HasAlternateKey(manufacturer => new { manufacturer.Id, manufacturer.TenantId });
    }
}