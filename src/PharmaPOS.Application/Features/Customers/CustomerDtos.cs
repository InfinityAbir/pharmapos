namespace PharmaPOS.Application.Features.Customers;

public sealed record CustomerDto(
    Guid Id,
    string Name,
    string? Phone,
    string? Email,
    string? Address,
    string? Notes,
    bool IsActive);

public sealed record CustomerHistoryItemDto(
    Guid SaleId,
    string InvoiceNumber,
    DateTimeOffset OccurredAtUtc,
    decimal TotalAmount);

public sealed record CustomerWithHistoryDto(
    CustomerDto Customer,
    IReadOnlyList<CustomerHistoryItemDto> RecentSales);
