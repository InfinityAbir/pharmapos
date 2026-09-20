using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Identity;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(role => role.Id);

        builder.Property(role => role.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(role => role.Name).HasMaxLength(64).IsRequired();
        builder.Property(role => role.Description).HasMaxLength(256);

        builder.HasIndex(role => new { role.TenantId, role.Name }).IsUnique();

        // Composite alternate key used by tenant-composite FKs (role_permissions, membership_roles),
        // which makes cross-tenant references structurally impossible.
        builder.HasAlternateKey(role => new { role.Id, role.TenantId });
    }
}