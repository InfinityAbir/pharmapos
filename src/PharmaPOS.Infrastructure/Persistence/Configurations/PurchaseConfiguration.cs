using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Purchasing;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.ToTable("purchases");

        builder.HasKey(purchase => purchase.Id);

        builder.Property(purchase => purchase.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(purchase => purchase.Status).HasMaxLength(16).IsRequired();
        builder.Property(purchase => purchase.SupplierInvoiceNumber).HasMaxLength(64);
        builder.Property(purchase => purchase.Notes).HasMaxLength(512);
        builder.Property(purchase => purchase.DiscountAmount).HasPrecision(18, 4);
        builder.Property(purchase => purchase.TaxAmount).HasPrecision(18, 4);
        builder.Property(purchase => purchase.TotalAmount).HasPrecision(18, 4);

        // Optimistic concurrency via PostgreSQL xmin (see architecture §4).
        builder.Property(purchase => purchase.Version).IsRowVersion();

        // Tenant-composite FK: a purchase always belongs to its own tenant's supplier.
        builder.HasOne(purchase => purchase.Supplier)
            .WithMany(supplier => supplier.Purchases)
            .HasForeignKey(purchase => new { purchase.SupplierId, purchase.TenantId })
            .HasPrincipalKey(supplier => new { supplier.Id, supplier.TenantId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(purchase => new { purchase.TenantId, purchase.SupplierId });
        builder.HasIndex(purchase => new { purchase.TenantId, purchase.Status });
        builder.HasIndex(purchase => new { purchase.TenantId, purchase.PurchaseDate });

        // Reporting: received purchases in a date range (totals + by-supplier).
        builder.HasIndex(purchase => new { purchase.TenantId, purchase.Status, purchase.PurchaseDate });

        // Composite alternate key used by tenant-composite FKs (items, idempotency keys).
        builder.HasAlternateKey(purchase => new { purchase.Id, purchase.TenantId });
    }
}