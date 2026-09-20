using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Identity;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions");

        builder.HasKey(rolePermission => rolePermission.Id);

        builder.Property(rolePermission => rolePermission.TenantId).HasMaxLength(64).IsRequired();

        // Composite FK: the role and the row must belong to the same tenant.
        builder.HasOne(rolePermission => rolePermission.Role)
            .WithMany(role => role.RolePermissions)
            .HasForeignKey(rolePermission => new { rolePermission.RoleId, rolePermission.TenantId })
            .HasPrincipalKey(role => new { role.Id, role.TenantId })
            .OnDelete(DeleteBehavior.Cascade);

        // The permission is platform-owned; only its id is referenced.
        builder.HasOne(rolePermission => rolePermission.Permission)
            .WithMany()
            .HasForeignKey(rolePermission => rolePermission.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(rolePermission => new { rolePermission.TenantId, rolePermission.RoleId });
        builder.HasIndex(rolePermission => new { rolePermission.RoleId, rolePermission.PermissionId }).IsUnique();
    }
}