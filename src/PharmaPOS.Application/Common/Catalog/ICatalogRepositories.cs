using PharmaPOS.Application.Features.Catalog;

namespace PharmaPOS.Application.Common.Catalog;

/// <summary>Tenant-scoped medicine categories.</summary>
public interface ICategoryRepository
{
    Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<CategoryDto> CreateAsync(
        string name,
        string? description,
        CancellationToken cancellationToken = default);

    Task<CategoryDto> UpdateAsync(
        Guid id,
        string name,
        string? description,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Tenant-scoped manufacturers.</summary>
public interface IManufacturerRepository
{
    Task<IReadOnlyList<ManufacturerDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<ManufacturerDto> CreateAsync(
        string name,
        string? country,
        CancellationToken cancellationToken = default);

    Task<ManufacturerDto> UpdateAsync(
        Guid id,
        string name,
        string? country,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Tenant-scoped pharmacy profile (single row per tenant; upsert semantics).</summary>
public interface IPharmacyProfileStore
{
    Task<PharmacyProfileDto?> GetAsync(CancellationToken cancellationToken = default);

    Task<PharmacyProfileDto> UpsertAsync(
        PharmacyProfileDto profile,
        CancellationToken cancellationToken = default);
}
