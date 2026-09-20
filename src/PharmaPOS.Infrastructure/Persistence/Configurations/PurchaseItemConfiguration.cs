using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Purchasing;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class PurchaseItemConfiguration : IEntityTypeConfiguration<PurchaseItem>
{
    public void Configure(EntityTypeBuilder<PurchaseItem> builder)
    {
        builder.ToTable("purchase_items");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(item => item.BatchNumber).HasMaxLength(64).IsRequired();
        builder.Property(item => item.UnitPrice).HasPrecision(18, 4);
        builder.Property(item => item.DiscountAmount).HasPrecision(18, 4);
        builder.Property(item => item.SellingPrice).HasPrecision(18, 4);

        // Tenant-composite FKs: items always reference their own tenant's rows.
        // Items live and die with their purchase (purchases themselves are never deleted).
        builder.HasOne(item => item.Purchase)
            .WithMany(purchase => purchase.Items)
            .HasForeignKey(item => new { item.PurchaseId, item.TenantId })
            .HasPrincipalKey(purchase => new { purchase.Id, purchase.TenantId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.Medicine)
            .WithMany()
            .HasForeignKey(item => new { item.MedicineId, item.TenantId })
            .HasPrincipalKey(medicine => new { medicine.Id, medicine.TenantId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.TenantId, item.PurchaseId });
        builder.HasIndex(item => new { item.TenantId, item.MedicineId });
    }
}