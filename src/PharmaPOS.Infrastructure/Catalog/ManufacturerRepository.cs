using Microsoft.EntityFrameworkCore;
using PharmaPOS.Application.Common.Catalog;
using PharmaPOS.Application.Features.Catalog;
using PharmaPOS.Domain.Catalog;
using PharmaPOS.Domain.Exceptions;
using PharmaPOS.Infrastructure.Persistence;

namespace PharmaPOS.Infrastructure.Catalog;

/// <summary>Tenant-scoped manufacturers.</summary>
public sealed class ManufacturerRepository : IManufacturerRepository
{
    private readonly AppDbContext _dbContext;

    public ManufacturerRepository(AppDbContext dbContext)
        => _dbContext = dbContext;

    public async Task<IReadOnlyList<ManufacturerDto>> ListAsync(CancellationToken cancellationToken)
    {
        var manufacturers = await _dbContext.Manufacturers
            .AsNoTracking()
            .OrderBy(manufacturer => manufacturer.Name)
            .ToListAsync(cancellationToken);

        return manufacturers
            .Select(manufacturer => new ManufacturerDto(
                manufacturer.Id, manufacturer.Name, manufacturer.Country))
            .ToArray();
    }

    public async Task<ManufacturerDto> CreateAsync(
        string name,
        string? country,
        CancellationToken cancellationToken)
    {
        name = name.Trim();

        if (await _dbContext.Manufacturers.AnyAsync(
            candidate => candidate.Name == name,
            cancellationToken))
        {
            throw new ConflictException(
                "manufacturer.duplicate",
                $"A manufacturer named '{name}' already exists.");
        }

        var manufacturer = new Manufacturer
        {
            TenantId = TenantScope.CurrentTenantId(_dbContext),
            Name = name,
            Country = Normalize(country),
        };

        _dbContext.Manufacturers.Add(manufacturer);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ManufacturerDto(manufacturer.Id, manufacturer.Name, manufacturer.Country);
    }

    public async Task<ManufacturerDto> UpdateAsync(
        Guid id,
        string name,
        string? country,
        CancellationToken cancellationToken)
    {
        name = name.Trim();

        var manufacturer = await _dbContext.Manufacturers
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (manufacturer is null)
        {
            throw new NotFoundException(
                "manufacturer.not_found",
                $"Manufacturer '{id}' was not found.");
        }

        if (manufacturer.Name != name
            && await _dbContext.Manufacturers.AnyAsync(
                candidate => candidate.Name == name && candidate.Id != id,
                cancellationToken))
        {
            throw new ConflictException(
                "manufacturer.duplicate",
                $"A manufacturer named '{name}' already exists.");
        }

        manufacturer.Name = name;
        manufacturer.Country = Normalize(country);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ManufacturerDto(manufacturer.Id, manufacturer.Name, manufacturer.Country);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var manufacturer = await _dbContext.Manufacturers
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (manufacturer is null)
        {
            throw new NotFoundException(
                "manufacturer.not_found",
                $"Manufacturer '{id}' was not found.");
        }

        if (await _dbContext.Medicines.AnyAsync(
            medicine => medicine.ManufacturerId == id,
            cancellationToken))
        {
            throw new ConflictException(
                "manufacturer.in_use",
                $"Manufacturer '{manufacturer.Name}' cannot be deleted while medicines reference it. " +
                "Reassign those medicines first, or keep the manufacturer.");
        }

        _dbContext.Manufacturers.Remove(manufacturer);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
