using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Sales;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("sales");

        builder.HasKey(sale => sale.Id);

        builder.Property(sale => sale.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(sale => sale.InvoiceNumber).HasMaxLength(32).IsRequired();
        builder.Property(sale => sale.Subtotal).HasPrecision(18, 4);
        builder.Property(sale => sale.DiscountAmount).HasPrecision(18, 4);
        builder.Property(sale => sale.TotalAmount).HasPrecision(18, 4);
        builder.Property(sale => sale.PaidAmount).HasPrecision(18, 4);
        builder.Property(sale => sale.CustomerName).HasMaxLength(160);
        builder.Property(sale => sale.CustomerPhone).HasMaxLength(64);
        builder.Property(sale => sale.CustomerId);
        builder.Property(sale => sale.PrescriptionReference).HasMaxLength(64);
        builder.Property(sale => sale.CashierSubject).HasMaxLength(320);

        // Optimistic concurrency via PostgreSQL xmin (see architecture §4).
        builder.Property(sale => sale.Version).IsRowVersion();

        builder.HasIndex(sale => new { sale.TenantId, sale.InvoiceNumber }).IsUnique();
        builder.HasIndex(sale => new { sale.TenantId, sale.OccurredAtUtc });

        // Reporting: sales grouped by cashier over a date range.
        builder.HasIndex(sale => new { sale.TenantId, sale.CashierSubject });

        // Composite alternate key used by tenant-composite FKs (items, payments, idempotency).
        builder.HasAlternateKey(sale => new { sale.Id, sale.TenantId });

        builder.HasIndex(sale => new { sale.TenantId, sale.CustomerId });
    }
}