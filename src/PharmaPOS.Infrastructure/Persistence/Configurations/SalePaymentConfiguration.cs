using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Sales;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class SalePaymentConfiguration : IEntityTypeConfiguration<SalePayment>
{
    public void Configure(EntityTypeBuilder<SalePayment> builder)
    {
        builder.ToTable("sale_payments");

        builder.HasKey(payment => payment.Id);

        builder.Property(payment => payment.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(payment => payment.Method).HasMaxLength(16).IsRequired();
        builder.Property(payment => payment.Amount).HasPrecision(18, 4);
        builder.Property(payment => payment.Reference).HasMaxLength(64);
        builder.Property(payment => payment.ReceivedBy).HasMaxLength(320);

        builder.HasOne(payment => payment.Sale)
            .WithMany(sale => sale.Payments)
            .HasForeignKey(payment => new { payment.SaleId, payment.TenantId })
            .HasPrincipalKey(sale => new { sale.Id, sale.TenantId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(payment => new { payment.TenantId, payment.SaleId });
    }
}