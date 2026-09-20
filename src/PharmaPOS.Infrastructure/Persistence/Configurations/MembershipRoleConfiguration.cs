using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Identity;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class MembershipRoleConfiguration : IEntityTypeConfiguration<MembershipRole>
{
    public void Configure(EntityTypeBuilder<MembershipRole> builder)
    {
        builder.ToTable("membership_roles");

        builder.HasKey(membershipRole => membershipRole.Id);

        builder.Property(membershipRole => membershipRole.TenantId).HasMaxLength(64).IsRequired();

        // Composite FKs require the membership and the role to belong to the same tenant.
        builder.HasOne(membershipRole => membershipRole.Membership)
            .WithMany(membership => membership.MembershipRoles)
            .HasForeignKey(membershipRole => new { membershipRole.MembershipId, membershipRole.TenantId })
            .HasPrincipalKey(membership => new { membership.Id, membership.TenantId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(membershipRole => membershipRole.Role)
            .WithMany(role => role.MembershipRoles)
            .HasForeignKey(membershipRole => new { membershipRole.RoleId, membershipRole.TenantId })
            .HasPrincipalKey(role => new { role.Id, role.TenantId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(membershipRole => new { membershipRole.TenantId, membershipRole.MembershipId });
        builder.HasIndex(membershipRole => new { membershipRole.MembershipId, membershipRole.RoleId }).IsUnique();
    }
}