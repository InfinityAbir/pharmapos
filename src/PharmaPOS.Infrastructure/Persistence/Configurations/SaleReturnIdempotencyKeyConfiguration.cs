using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Sales;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class SaleReturnIdempotencyKeyConfiguration : IEntityTypeConfiguration<SaleReturnIdempotencyKey>
{
    public void Configure(EntityTypeBuilder<SaleReturnIdempotencyKey> builder)
    {
        builder.ToTable("sale_return_idempotency_keys");

        builder.HasKey(key => key.Id);

        builder.Property(key => key.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(key => key.Key).HasMaxLength(128).IsRequired();

        builder.HasOne(key => key.SaleReturn)
            .WithMany()
            .HasForeignKey(key => new { key.SaleReturnId, key.TenantId })
            .HasPrincipalKey(saleReturn => new { saleReturn.Id, saleReturn.TenantId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(key => new { key.TenantId, key.Key }).IsUnique();
    }
}
