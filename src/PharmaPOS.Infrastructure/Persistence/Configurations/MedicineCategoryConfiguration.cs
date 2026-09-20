using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Catalog;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class MedicineCategoryConfiguration : IEntityTypeConfiguration<MedicineCategory>
{
    public void Configure(EntityTypeBuilder<MedicineCategory> builder)
    {
        builder.ToTable("medicine_categories");

        builder.HasKey(category => category.Id);

        builder.Property(category => category.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(category => category.Name).HasMaxLength(100).IsRequired();
        builder.Property(category => category.Description).HasMaxLength(256);

        builder.HasIndex(category => new { category.TenantId, category.Name }).IsUnique();

        // Composite alternate key used by the tenant-composite FK from medicines.
        builder.HasAlternateKey(category => new { category.Id, category.TenantId });
    }
}