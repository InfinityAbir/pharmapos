using PharmaPOS.Application.Features.Tenancy;

namespace PharmaPOS.Application.Common.Tenancy;

/// <summary>
/// Platform-level tenant directory (Finbuckle <c>TenantInfo</c> records). Deliberately separate from
/// the tenant-filtered application context: listing, creating and suspending tenants is a platform
/// concern that must not be scoped to the caller's tenant.
/// </summary>
public interface IPlatformTenantStore
{
    Task<IReadOnlyList<TenantDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<TenantDto?> FindByIdentifierAsync(
        string identifier,
        CancellationToken cancellationToken = default);

    Task<TenantDto> CreateAsync(TenantDto tenant, CancellationToken cancellationToken = default);

    Task<TenantDto> UpdateAsync(TenantDto tenant, CancellationToken cancellationToken = default);
}