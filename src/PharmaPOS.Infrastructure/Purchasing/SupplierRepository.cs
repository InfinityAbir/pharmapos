using Microsoft.EntityFrameworkCore;
using PharmaPOS.Application.Common.Purchasing;
using PharmaPOS.Application.Features.Purchasing;
using PharmaPOS.Domain.Exceptions;
using PharmaPOS.Domain.Purchasing;
using PharmaPOS.Infrastructure.Persistence;

namespace PharmaPOS.Infrastructure.Purchasing;

/// <summary>Tenant-scoped suppliers.</summary>
public sealed class SupplierRepository : ISupplierRepository
{
    private readonly AppDbContext _dbContext;

    public SupplierRepository(AppDbContext dbContext)
        => _dbContext = dbContext;

    public async Task<IReadOnlyList<SupplierDto>> ListAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Suppliers
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLike(search.Trim())}%";
            query = query.Where(supplier =>
                EF.Functions.ILike(supplier.Name, pattern)
                || (supplier.Company != null && EF.Functions.ILike(supplier.Company, pattern)));
        }

        var suppliers = await query
            .OrderBy(supplier => supplier.Name)
            .ToListAsync(cancellationToken);

        return suppliers.Select(ToDto).ToArray();
    }

    public async Task<SupplierDto?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await _dbContext.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return supplier is null ? null : ToDto(supplier);
    }

    public async Task<SupplierDto> CreateAsync(
        SupplierInput input,
        CancellationToken cancellationToken)
    {
        var name = input.Name.Trim();

        if (await _dbContext.Suppliers.AnyAsync(
            candidate => candidate.Name == name,
            cancellationToken))
        {
            throw new ConflictException(
                "supplier.duplicate",
                $"A supplier named '{name}' already exists.");
        }

        var supplier = new Supplier
        {
            TenantId = TenantScope.CurrentTenantId(_dbContext),
            Name = name,
            Company = Normalize(input.Company),
            Phone = Normalize(input.Phone),
            Email = Normalize(input.Email)?.ToLowerInvariant(),
            Address = Normalize(input.Address),
            ContactPerson = Normalize(input.ContactPerson),
            PaymentTerms = Normalize(input.PaymentTerms),
            IsActive = true,
        };

        _dbContext.Suppliers.Add(supplier);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(supplier);
    }

    public async Task<SupplierDto> UpdateAsync(
        Guid id,
        SupplierInput input,
        CancellationToken cancellationToken)
    {
        var name = input.Name.Trim();

        var supplier = await _dbContext.Suppliers
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (supplier is null)
        {
            throw new NotFoundException("supplier.not_found", $"Supplier '{id}' was not found.");
        }

        if (supplier.Name != name
            && await _dbContext.Suppliers.AnyAsync(
                candidate => candidate.Name == name && candidate.Id != id,
                cancellationToken))
        {
            throw new ConflictException(
                "supplier.duplicate",
                $"A supplier named '{name}' already exists.");
        }

        supplier.Name = name;
        supplier.Company = Normalize(input.Company);
        supplier.Phone = Normalize(input.Phone);
        supplier.Email = Normalize(input.Email)?.ToLowerInvariant();
        supplier.Address = Normalize(input.Address);
        supplier.ContactPerson = Normalize(input.ContactPerson);
        supplier.PaymentTerms = Normalize(input.PaymentTerms);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(supplier);
    }

    public async Task<SupplierDto> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var supplier = await _dbContext.Suppliers
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (supplier is null)
        {
            throw new NotFoundException("supplier.not_found", $"Supplier '{id}' was not found.");
        }

        supplier.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(supplier);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await _dbContext.Suppliers
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (supplier is null)
        {
            throw new NotFoundException("supplier.not_found", $"Supplier '{id}' was not found.");
        }

        if (await _dbContext.Purchases.AnyAsync(
            purchase => purchase.SupplierId == id,
            cancellationToken))
        {
            throw new ConflictException(
                "supplier.in_use",
                $"Supplier '{supplier.Name}' cannot be deleted while purchases reference it. " +
                "Deactivate it instead.");
        }

        _dbContext.Suppliers.Remove(supplier);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static SupplierDto ToDto(Supplier supplier)
        => new(
            Id: supplier.Id,
            Name: supplier.Name,
            Company: supplier.Company,
            Phone: supplier.Phone,
            Email: supplier.Email,
            Address: supplier.Address,
            ContactPerson: supplier.ContactPerson,
            PaymentTerms: supplier.PaymentTerms,
            IsActive: supplier.IsActive);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string EscapeLike(string value)
        => value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
