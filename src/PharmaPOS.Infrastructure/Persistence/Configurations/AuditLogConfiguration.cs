using PharmaPOS.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PharmaPOS.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(audit => audit.Id);

        builder.Property(audit => audit.TenantId)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(audit => audit.ActorSubject).HasMaxLength(128);
        builder.Property(audit => audit.Action).HasMaxLength(128).IsRequired();
        builder.Property(audit => audit.EntityType).HasMaxLength(128);
        builder.Property(audit => audit.EntityId).HasMaxLength(128);
        builder.Property(audit => audit.Data).HasColumnType("jsonb");
        builder.Property(audit => audit.IpAddress).HasMaxLength(64);
        builder.Property(audit => audit.CorrelationId).HasMaxLength(128);
        builder.Property(audit => audit.OccurredAtUtc).IsRequired();

        // Tenant-leading indexes keep per-tenant audit queries efficient.
        builder.HasIndex(audit => new { audit.TenantId, audit.OccurredAtUtc });
        builder.HasIndex(audit => new { audit.TenantId, audit.Action });
    }
}
