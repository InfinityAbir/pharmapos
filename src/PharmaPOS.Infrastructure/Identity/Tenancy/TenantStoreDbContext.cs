using Finbuckle.MultiTenant.EntityFrameworkCore.Stores;
using Microsoft.EntityFrameworkCore;

namespace PharmaPOS.Infrastructure.Identity.Tenancy;

/// <summary>
/// Dedicated store context for tenant metadata (Finbuckle <c>EFCoreStore</c>). It is deliberately
/// separate from <c>AppDbContext</c>: the store must be able to read tenant information *before*
/// a tenant context exists, so it cannot itself be tenant-filtered.
/// </summary>
public sealed class TenantStoreDbContext : EFCoreStoreDbContext<AppTenantInfo>
{
    public TenantStoreDbContext(DbContextOptions<TenantStoreDbContext> options)
        : base(options)
    {
    }
}
