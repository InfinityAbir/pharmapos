using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using PharmaPOS.Domain.Auditing;

namespace PharmaPOS.Infrastructure.Persistence;

/// <summary>
/// The application's tenant-aware EF Core context.
///
/// Derives from Finbuckle's <see cref="MultiTenantDbContext"/>: every entity designated as
/// multi-tenant gets a managed <c>TenantId</c>, automatic global query filtering (reads) and
/// mismatch enforcement on save (writes). The current tenant is bound at construction and is
/// immutable for the lifetime of the context, which is what makes isolation trustworthy.
///
/// Phase 0 maps only <see cref="AuditLog"/>. Business entities arrive in later phases and are
/// marked multi-tenant the same way.
/// </summary>
public sealed class AppDbContext : MultiTenantDbContext
{
    public AppDbContext(
        IMultiTenantContextAccessor multiTenantContextAccessor,
        DbContextOptions<AppDbContext> options)
        : base(multiTenantContextAccessor, options)
    {
    }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<AuditLog>().IsMultiTenant();
    }
}
