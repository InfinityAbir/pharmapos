using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Sales;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> builder)
    {
        builder.ToTable("sale_items");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(item => item.UnitPrice).HasPrecision(18, 4);
        builder.Property(item => item.DiscountAmount).HasPrecision(18, 4);

        // Tenant-composite FKs: an item always references its own tenant's rows.
        builder.HasOne(item => item.Sale)
            .WithMany(sale => sale.Items)
            .HasForeignKey(item => new { item.SaleId, item.TenantId })
            .HasPrincipalKey(sale => new { sale.Id, sale.TenantId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.Medicine)
            .WithMany()
            .HasForeignKey(item => new { item.MedicineId, item.TenantId })
            .HasPrincipalKey(medicine => new { medicine.Id, medicine.TenantId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.Batch)
            .WithMany()
            .HasForeignKey(item => new { item.BatchId, item.TenantId })
            .HasPrincipalKey(batch => new { batch.Id, batch.TenantId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.TenantId, item.SaleId });
        builder.HasIndex(item => new { item.TenantId, item.MedicineId });
        builder.HasIndex(item => new { item.TenantId, item.BatchId });
    }
}