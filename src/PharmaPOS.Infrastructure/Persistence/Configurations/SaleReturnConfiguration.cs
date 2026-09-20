using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Sales;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class SaleReturnConfiguration : IEntityTypeConfiguration<SaleReturn>
{
    public void Configure(EntityTypeBuilder<SaleReturn> builder)
    {
        builder.ToTable("sale_returns");

        builder.HasKey(saleReturn => saleReturn.Id);

        builder.Property(saleReturn => saleReturn.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(saleReturn => saleReturn.Reason).HasMaxLength(512).IsRequired();
        builder.Property(saleReturn => saleReturn.RefundMethod).HasMaxLength(32).IsRequired();
        builder.Property(saleReturn => saleReturn.RefundReference).HasMaxLength(64);
        builder.Property(saleReturn => saleReturn.RefundAmount).HasPrecision(18, 4);
        builder.Property(saleReturn => saleReturn.ActorSubject).HasMaxLength(320);

        // Tenant-composite FK: a return always references its own tenant's sale.
        builder.HasOne(saleReturn => saleReturn.Sale)
            .WithMany()
            .HasForeignKey(saleReturn => new { saleReturn.SaleId, saleReturn.TenantId })
            .HasPrincipalKey(sale => new { sale.Id, sale.TenantId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasAlternateKey(saleReturn => new { saleReturn.Id, saleReturn.TenantId });

        builder.HasIndex(saleReturn => new { saleReturn.TenantId, saleReturn.SaleId });
        builder.HasIndex(saleReturn => new { saleReturn.TenantId, saleReturn.OccurredAtUtc });
    }
}
