using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Catalog;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class MedicineConfiguration : IEntityTypeConfiguration<Medicine>
{
    public void Configure(EntityTypeBuilder<Medicine> builder)
    {
        builder.ToTable("medicines");

        builder.HasKey(medicine => medicine.Id);

        builder.Property(medicine => medicine.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(medicine => medicine.BrandName).HasMaxLength(160).IsRequired();
        builder.Property(medicine => medicine.GenericName).HasMaxLength(160).IsRequired();
        builder.Property(medicine => medicine.Strength).HasMaxLength(64).IsRequired();
        builder.Property(medicine => medicine.DosageForm).HasMaxLength(32).IsRequired();
        builder.Property(medicine => medicine.Barcode).HasMaxLength(64);
        builder.Property(medicine => medicine.Unit).HasMaxLength(32).IsRequired();
        builder.Property(medicine => medicine.PurchasePrice).HasPrecision(18, 4);
        builder.Property(medicine => medicine.SellingPrice).HasPrecision(18, 4);
        builder.Property(medicine => medicine.VatRate).HasPrecision(5, 2);

        // Optimistic concurrency via PostgreSQL xmin (see architecture §4).
        builder.Property(medicine => medicine.Version).IsRowVersion();

        // Tenant-composite FKs: a medicine can only reference its own tenant's
        // manufacturer and category. Deletion of a referenced row is refused.
        builder.HasOne(medicine => medicine.Manufacturer)
            .WithMany(manufacturer => manufacturer.Medicines)
            .HasForeignKey(medicine => new { medicine.ManufacturerId, medicine.TenantId })
            .HasPrincipalKey(manufacturer => new { manufacturer.Id, manufacturer.TenantId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(medicine => medicine.Category)
            .WithMany(category => category.Medicines)
            .HasForeignKey(medicine => new { medicine.CategoryId, medicine.TenantId })
            .HasPrincipalKey(category => new { category.Id, category.TenantId })
            .OnDelete(DeleteBehavior.Restrict);

        // Barcode unique per tenant when present (PostgreSQL allows multiple NULLs).
        builder.HasIndex(medicine => new { medicine.TenantId, medicine.Barcode }).IsUnique();

        // Tenant-leading search indexes.
        builder.HasIndex(medicine => new { medicine.TenantId, medicine.BrandName });
        builder.HasIndex(medicine => new { medicine.TenantId, medicine.GenericName });
    }
}