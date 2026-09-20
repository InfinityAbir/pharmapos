using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using PharmaPOS.Domain.Auditing;
using PharmaPOS.Domain.Catalog;
using PharmaPOS.Domain.Identity;

namespace PharmaPOS.Infrastructure.Persistence;

/// <summary>
/// The application's tenant-aware EF Core context.
///
/// Derives from Finbuckle's <see cref="MultiTenantDbContext"/>: every entity designated as
/// multi-tenant gets a managed <c>TenantId</c>, automatic global query filtering (reads) and
/// mismatch enforcement on save (writes). The current tenant is bound at construction and is
/// immutable for the lifetime of the context, which is what makes isolation trustworthy.
///
/// Phase 1 adds the identity graph: users, tenant memberships, roles and permissions. Roles,
/// memberships and their join rows are tenant-scoped; users and the permission catalog are
/// platform-owned.
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
    public DbSet<ApplicationUser> ApplicationUsers => Set<ApplicationUser>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<MembershipRole> MembershipRoles => Set<MembershipRole>();
    public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();
    public DbSet<MedicineCategory> MedicineCategories => Set<MedicineCategory>();
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<PharmacyProfile> PharmacyProfiles => Set<PharmacyProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<AuditLog>().IsMultiTenant();
        modelBuilder.Entity<TenantMembership>().IsMultiTenant();
        modelBuilder.Entity<Role>().IsMultiTenant();
        modelBuilder.Entity<RolePermission>().IsMultiTenant();
        modelBuilder.Entity<MembershipRole>().IsMultiTenant();
        modelBuilder.Entity<Manufacturer>().IsMultiTenant();
        modelBuilder.Entity<MedicineCategory>().IsMultiTenant();
        modelBuilder.Entity<Medicine>().IsMultiTenant();
        modelBuilder.Entity<PharmacyProfile>().IsMultiTenant();
    }
}
