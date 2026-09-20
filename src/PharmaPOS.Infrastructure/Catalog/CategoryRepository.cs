using Microsoft.EntityFrameworkCore;
using PharmaPOS.Application.Common.Catalog;
using PharmaPOS.Application.Features.Catalog;
using PharmaPOS.Domain.Catalog;
using PharmaPOS.Domain.Exceptions;
using PharmaPOS.Infrastructure.Persistence;

namespace PharmaPOS.Infrastructure.Catalog;

/// <summary>Tenant-scoped medicine categories.</summary>
public sealed class CategoryRepository : ICategoryRepository
{
    private readonly AppDbContext _dbContext;

    public CategoryRepository(AppDbContext dbContext)
        => _dbContext = dbContext;

    public async Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken cancellationToken)
    {
        var categories = await _dbContext.MedicineCategories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .ToListAsync(cancellationToken);

        return categories
            .Select(category => new CategoryDto(category.Id, category.Name, category.Description))
            .ToArray();
    }

    public async Task<CategoryDto> CreateAsync(
        string name,
        string? description,
        CancellationToken cancellationToken)
    {
        name = name.Trim();

        if (await _dbContext.MedicineCategories.AnyAsync(
            candidate => candidate.Name == name,
            cancellationToken))
        {
            throw new ConflictException(
                "category.duplicate",
                $"A category named '{name}' already exists.");
        }

        var category = new MedicineCategory
        {
            TenantId = TenantScope.CurrentTenantId(_dbContext),
            Name = name,
            Description = Normalize(description),
        };

        _dbContext.MedicineCategories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CategoryDto(category.Id, category.Name, category.Description);
    }

    public async Task<CategoryDto> UpdateAsync(
        Guid id,
        string name,
        string? description,
        CancellationToken cancellationToken)
    {
        name = name.Trim();

        var category = await _dbContext.MedicineCategories
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (category is null)
        {
            throw new NotFoundException("category.not_found", $"Category '{id}' was not found.");
        }

        if (category.Name != name
            && await _dbContext.MedicineCategories.AnyAsync(
                candidate => candidate.Name == name && candidate.Id != id,
                cancellationToken))
        {
            throw new ConflictException(
                "category.duplicate",
                $"A category named '{name}' already exists.");
        }

        category.Name = name;
        category.Description = Normalize(description);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CategoryDto(category.Id, category.Name, category.Description);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await _dbContext.MedicineCategories
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (category is null)
        {
            throw new NotFoundException("category.not_found", $"Category '{id}' was not found.");
        }

        if (await _dbContext.Medicines.AnyAsync(
            medicine => medicine.CategoryId == id,
            cancellationToken))
        {
            throw new ConflictException(
                "category.in_use",
                $"Category '{category.Name}' cannot be deleted while medicines reference it. " +
                "Reassign those medicines first, or keep the category.");
        }

        _dbContext.MedicineCategories.Remove(category);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
