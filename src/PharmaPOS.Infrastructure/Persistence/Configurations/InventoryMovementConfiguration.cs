using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Inventory;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable("inventory_movements");

        builder.HasKey(movement => movement.Id);

        builder.Property(movement => movement.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(movement => movement.Type).HasMaxLength(32).IsRequired();
        builder.Property(movement => movement.Reason).HasMaxLength(256);
        builder.Property(movement => movement.ActorSubject).HasMaxLength(320);

        // Tenant-composite FKs: a movement always references its own tenant's rows.
        // The ledger is append-only; Restrict guards against misuse.
        builder.HasOne(movement => movement.Medicine)
            .WithMany()
            .HasForeignKey(movement => new { movement.MedicineId, movement.TenantId })
            .HasPrincipalKey(medicine => new { medicine.Id, medicine.TenantId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(movement => movement.Batch)
            .WithMany()
            .HasForeignKey(movement => new { movement.BatchId, movement.TenantId })
            .HasPrincipalKey(batch => new { batch.Id, batch.TenantId })
            .OnDelete(DeleteBehavior.Restrict);

        // Tenant-leading ledger indexes (newest-first reads).
        builder.HasIndex(movement => new { movement.TenantId, movement.MedicineId });
        builder.HasIndex(movement => new { movement.TenantId, movement.BatchId });
        builder.HasIndex(movement => new { movement.TenantId, movement.OccurredAtUtc });
    }
}