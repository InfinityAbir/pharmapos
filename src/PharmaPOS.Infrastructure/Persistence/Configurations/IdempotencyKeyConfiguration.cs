using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Purchasing;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.ToTable("idempotency_keys");

        builder.HasKey(key => key.Id);

        builder.Property(key => key.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(key => key.Key).HasMaxLength(128).IsRequired();

        builder.HasIndex(key => new { key.TenantId, key.Key }).IsUnique();

        builder.HasOne<Purchase>()
            .WithMany()
            .HasForeignKey(key => new { key.PurchaseId, key.TenantId })
            .HasPrincipalKey(purchase => new { purchase.Id, purchase.TenantId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}