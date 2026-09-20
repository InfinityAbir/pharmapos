using Microsoft.EntityFrameworkCore;
using PharmaPOS.Application.Common.Customers;
using PharmaPOS.Application.Features.Customers;
using PharmaPOS.Domain.Customers;
using PharmaPOS.Domain.Exceptions;
using PharmaPOS.Infrastructure.Persistence;

namespace PharmaPOS.Infrastructure.Customers;

/// <summary>Tenant-scoped customers with phone-uniqueness per tenant (when phone is set).</summary>
public sealed class CustomerRepository : ICustomerRepository
{
    private readonly AppDbContext _dbContext;

    public CustomerRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<CustomerDto>> ListAsync(
        string? search,
        bool? isActive,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(customer =>
                EF.Functions.ILike(customer.Name, $"%{term}%")
                || (customer.Phone != null && EF.Functions.ILike(customer.Phone, $"%{term}%")));
        }

        if (isActive.HasValue)
        {
            query = query.Where(customer => customer.IsActive == isActive.Value);
        }

        var customers = await query
            .OrderBy(customer => customer.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return customers.Select(ToDto).ToArray();
    }

    public async Task<CustomerDto?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return customer is null ? null : ToDto(customer);
    }

    public async Task<CustomerWithHistoryDto?> FindWithHistoryAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (customer is null)
        {
            return null;
        }

        var sales = await _dbContext.Sales
            .AsNoTracking()
            .Where(sale => sale.CustomerId == id)
            .OrderByDescending(sale => sale.OccurredAtUtc)
            .Take(25)
            .Select(sale => new CustomerHistoryItemDto(
                sale.Id, sale.InvoiceNumber, sale.OccurredAtUtc, sale.TotalAmount))
            .ToListAsync(cancellationToken);

        return new CustomerWithHistoryDto(ToDto(customer), sales);
    }

    public async Task<CustomerDto> CreateAsync(
        CustomerInput input,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantScope.CurrentTenantId(_dbContext);
        var name = input.Name.Trim();
        var phone = Normalize(input.Phone);
        var email = Normalize(input.Email);

        await EnsurePhoneUniqueAsync(null, phone, cancellationToken);

        var customer = new Customer
        {
            TenantId = tenantId,
            Name = name,
            Phone = phone,
            Email = email,
            Address = Normalize(input.Address),
            Notes = Normalize(input.Notes),
            IsActive = true,
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(customer);
    }

    public async Task<CustomerDto> UpdateAsync(
        Guid id,
        CustomerInput input,
        CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (customer is null)
        {
            throw new NotFoundException("customer.not_found", "Customer not found in this tenant.");
        }

        var phone = Normalize(input.Phone);
        await EnsurePhoneUniqueAsync(id, phone, cancellationToken);

        customer.Name = input.Name.Trim();
        customer.Phone = phone;
        customer.Email = Normalize(input.Email);
        customer.Address = Normalize(input.Address);
        customer.Notes = Normalize(input.Notes);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(customer);
    }

    public async Task<CustomerDto> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (customer is null)
        {
            throw new NotFoundException("customer.not_found", "Customer not found in this tenant.");
        }

        customer.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(customer);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (customer is null)
        {
            throw new NotFoundException("customer.not_found", "Customer not found in this tenant.");
        }

        var referenced = await _dbContext.Sales
            .AnyAsync(sale => sale.CustomerId == id, cancellationToken);

        if (referenced)
        {
            throw new BusinessRuleException(
                "customer.has_sales",
                "This customer has sales linked and cannot be deleted. Deactivate it instead.");
        }

        _dbContext.Customers.Remove(customer);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsurePhoneUniqueAsync(
        Guid? excludeId,
        string? phone,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return;
        }

        var clash = await _dbContext.Customers
            .AsNoTracking()
            .AnyAsync(
                candidate => candidate.Phone == phone && candidate.Id != excludeId,
                cancellationToken);

        if (clash)
        {
            throw new ConflictException(
                "customer.phone_exists",
                $"Another customer already uses phone '{phone}'.");
        }
    }

    private static CustomerDto ToDto(Customer customer)
        => new(
            customer.Id, customer.Name, customer.Phone, customer.Email,
            customer.Address, customer.Notes, customer.IsActive);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
