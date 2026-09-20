using PharmaPOS.Application.Features.Tenancy;

namespace PharmaPOS.Application.Common.Tenancy;

/// <summary>
/// Manages the current tenant's roles. All operations are scoped to the current tenant by the
/// tenant-filtered context; implementations reject cross-tenant references.
/// </summary>
public interface ITenantRoleRepository
{
    Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<RoleDto> CreateAsync(
        string name,
        string? description,
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken = default);

    Task<RoleDto> UpdateAsync(
        Guid id,
        string name,
        string? description,
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}