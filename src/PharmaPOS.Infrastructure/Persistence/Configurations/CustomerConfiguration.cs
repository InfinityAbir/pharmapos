using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Customers;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(customer => customer.Name).HasMaxLength(160).IsRequired();
        builder.Property(customer => customer.Phone).HasMaxLength(64);
        builder.Property(customer => customer.Email).HasMaxLength(320);
        builder.Property(customer => customer.Address).HasMaxLength(512);
        builder.Property(customer => customer.Notes).HasMaxLength(1024);

        // Composite alternate key used by tenant-composite FKs (sales).
        builder.HasAlternateKey(customer => new { customer.Id, customer.TenantId });

        builder.HasIndex(customer => new { customer.TenantId, customer.Name });
        builder.HasIndex(customer => new { customer.TenantId, customer.Phone });
        builder.HasIndex(customer => new { customer.TenantId, customer.IsActive });
    }
}
