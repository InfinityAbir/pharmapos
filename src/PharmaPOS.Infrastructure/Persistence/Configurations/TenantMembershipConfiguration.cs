using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaPOS.Domain.Identity;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.ToTable("tenant_memberships");

        builder.HasKey(membership => membership.Id);

        builder.Property(membership => membership.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(membership => membership.Status).HasMaxLength(16).IsRequired();

        builder.HasIndex(membership => new { membership.TenantId, membership.ApplicationUserId }).IsUnique();
        builder.HasIndex(membership => new { membership.TenantId, membership.Status });

        // Composite alternate key used by tenant-composite FKs (membership_roles).
        builder.HasAlternateKey(membership => new { membership.Id, membership.TenantId });

        builder.HasOne(membership => membership.ApplicationUser)
            .WithMany()
            .HasForeignKey(membership => membership.ApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}