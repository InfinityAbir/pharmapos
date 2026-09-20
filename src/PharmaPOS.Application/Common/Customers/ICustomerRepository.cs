using PharmaPOS.Application.Features.Customers;

namespace PharmaPOS.Application.Common.Customers;

/// <summary>Tenant-scoped customers: optional for cash sales, linked for history.</summary>
public interface ICustomerRepository
{
    Task<IReadOnlyList<CustomerDto>> ListAsync(
        string? search,
        bool? isActive,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<CustomerDto?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CustomerWithHistoryDto?> FindWithHistoryAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<CustomerDto> CreateAsync(
        CustomerInput input,
        CancellationToken cancellationToken = default);

    Task<CustomerDto> UpdateAsync(
        Guid id,
        CustomerInput input,
        CancellationToken cancellationToken = default);

    Task<CustomerDto> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record CustomerInput(
    string Name,
    string? Phone,
    string? Email,
    string? Address,
    string? Notes);
