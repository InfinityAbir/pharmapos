using System;

namespace PharmaPOS.Infrastructure.Persistence;

/// <summary>
/// Reads the tenant bound to an <see cref="AppDbContext"/> for explicit <c>TenantId</c> assignment.
///
/// Finbuckle stamps <c>TenantId</c> at save time, but entities behind tenant-composite alternate keys
/// (roles, memberships and their joins) must carry a non-null <c>TenantId</c> already when they
/// start being tracked — EF Core rejects null alternate keys on <c>Add</c>. Every explicit
/// assignment here mirrors what Finbuckle would stamp, so save-time mismatch enforcement still holds.
/// </summary>
internal static class TenantScope
{
    public static string CurrentTenantId(AppDbContext db)
        => db.TenantInfo?.Id
        ?? throw new InvalidOperationException(
            "No tenant is resolved for the current context. Tenant-scoped writes require a bound tenant.");
}
