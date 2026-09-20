using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Purchasing;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers");

        builder.HasKey(supplier => supplier.Id);

        builder.Property(supplier => supplier.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(supplier => supplier.Name).HasMaxLength(128).IsRequired();
        builder.Property(supplier => supplier.Company).HasMaxLength(160);
        builder.Property(supplier => supplier.Phone).HasMaxLength(64);
        builder.Property(supplier => supplier.Email).HasMaxLength(320);
        builder.Property(supplier => supplier.Address).HasMaxLength(512);
        builder.Property(supplier => supplier.ContactPerson).HasMaxLength(160);
        builder.Property(supplier => supplier.PaymentTerms).HasMaxLength(256);

        builder.HasIndex(supplier => new { supplier.TenantId, supplier.Name }).IsUnique();

        // Composite alternate key used by the tenant-composite FK from purchases.
        builder.HasAlternateKey(supplier => new { supplier.Id, supplier.TenantId });
    }
}