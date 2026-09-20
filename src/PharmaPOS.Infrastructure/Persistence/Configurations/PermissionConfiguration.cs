using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Identity;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");

        builder.HasKey(permission => permission.Id);

        builder.Property(permission => permission.Code).HasMaxLength(128).IsRequired();
        builder.HasIndex(permission => permission.Code).IsUnique();

        builder.Property(permission => permission.Module).HasMaxLength(64).IsRequired();
        builder.Property(permission => permission.Description).HasMaxLength(256);
    }
}