using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Sales;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class InvoiceCounterConfiguration : IEntityTypeConfiguration<InvoiceCounter>
{
    public void Configure(EntityTypeBuilder<InvoiceCounter> builder)
    {
        builder.ToTable("invoice_counters");

        builder.HasKey(counter => counter.TenantId);

        builder.Property(counter => counter.TenantId).HasMaxLength(64).IsRequired();
    }
}