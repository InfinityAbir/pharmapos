using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Sales;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class SaleReturnItemConfiguration : IEntityTypeConfiguration<SaleReturnItem>
{
    public void Configure(EntityTypeBuilder<SaleReturnItem> builder)
    {
        builder.ToTable("sale_return_items");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(item => item.UnitPrice).HasPrecision(18, 4);
        builder.Property(item => item.RefundAmount).HasPrecision(18, 4);

        builder.HasOne(item => item.SaleReturn)
            .WithMany(saleReturn => saleReturn.Items)
            .HasForeignKey(item => new { item.SaleReturnId, item.TenantId })
            .HasPrincipalKey(saleReturn => new { saleReturn.Id, saleReturn.TenantId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.SaleItem)
            .WithMany()
            .HasForeignKey(item => new { item.SaleItemId, item.TenantId })
            .HasPrincipalKey(saleItem => new { saleItem.Id, saleItem.TenantId })
            .OnDelete(DeleteBehavior.Restrict);

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

        builder.HasIndex(item => new { item.TenantId, item.SaleReturnId });
        builder.HasIndex(item => new { item.TenantId, item.SaleItemId });
    }
}
