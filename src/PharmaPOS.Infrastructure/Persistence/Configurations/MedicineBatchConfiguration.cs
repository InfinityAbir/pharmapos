using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Inventory;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class MedicineBatchConfiguration : IEntityTypeConfiguration<MedicineBatch>
{
    public void Configure(EntityTypeBuilder<MedicineBatch> builder)
    {
        builder.ToTable("medicine_batches");

        builder.HasKey(batch => batch.Id);

        builder.Property(batch => batch.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(batch => batch.BatchNumber).HasMaxLength(64).IsRequired();
        builder.Property(batch => batch.PurchasePrice).HasPrecision(18, 4);
        builder.Property(batch => batch.SellingPrice).HasPrecision(18, 4);
        builder.Property(batch => batch.SupplierName).HasMaxLength(160);
        builder.Property(batch => batch.PurchaseReference).HasMaxLength(64);

        // Optimistic concurrency via PostgreSQL xmin (see architecture §4).
        builder.Property(batch => batch.Version).IsRowVersion();

        // Tenant-composite FK: a batch always belongs to its own tenant's medicine.
        // Medicines are never deleted, so Restrict only guards against misuse.
        builder.HasOne(batch => batch.Medicine)
            .WithMany()
            .HasForeignKey(batch => new { batch.MedicineId, batch.TenantId })
            .HasPrincipalKey(medicine => new { medicine.Id, medicine.TenantId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(batch => new { batch.TenantId, batch.MedicineId, batch.BatchNumber })
            .IsUnique();

        // Tenant-leading operational indexes.
        builder.HasIndex(batch => new { batch.TenantId, batch.MedicineId });
        builder.HasIndex(batch => new { batch.TenantId, batch.ExpiryDate });

        // Composite alternate key used by the tenant-composite FK from movements.
        builder.HasAlternateKey(batch => new { batch.Id, batch.TenantId });
    }
}