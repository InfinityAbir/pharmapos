using Finbuckle.MultiTenant.Abstractions;

namespace PharmaPOS.Infrastructure.Identity.Tenancy;

/// <summary>
/// Application tenant descriptor. Extends Finbuckle's <see cref="TenantInfo"/> with
/// pharmacy/organization metadata. Stored in the application database and loaded through
/// Finbuckle's EF Core store — the application never invents its own tenant resolution.
/// </summary>
public sealed class AppTenantInfo : TenantInfo
{
    /// <summary>Lifecycle state of the tenant (e.g. <c>Active</c>, <c>Suspended</c>).</summary>
    public string Status { get; set; } = "Active";

    /// <summary>Subscription plan code, reserved for the SaaS billing phase.</summary>
    public string? PlanCode { get; set; }
}
