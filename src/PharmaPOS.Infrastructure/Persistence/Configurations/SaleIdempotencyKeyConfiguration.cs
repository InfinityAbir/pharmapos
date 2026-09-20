using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Sales;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class SaleIdempotencyKeyConfiguration : IEntityTypeConfiguration<SaleIdempotencyKey>
{
    public void Configure(EntityTypeBuilder<SaleIdempotencyKey> builder)
    {
        builder.ToTable("sale_idempotency_keys");

        builder.HasKey(key => key.Id);

        builder.Property(key => key.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(key => key.Key).HasMaxLength(128).IsRequired();

        builder.HasIndex(key => new { key.TenantId, key.Key }).IsUnique();

        builder.HasOne<Sale>()
            .WithMany()
            .HasForeignKey(key => new { key.SaleId, key.TenantId })
            .HasPrincipalKey(sale => new { sale.Id, sale.TenantId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}