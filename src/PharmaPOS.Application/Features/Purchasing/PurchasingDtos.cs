namespace PharmaPOS.Application.Features.Purchasing;

public sealed record SupplierDto(
    Guid Id,
    string Name,
    string? Company,
    string? Phone,
    string? Email,
    string? Address,
    string? ContactPerson,
    string? PaymentTerms,
    bool IsActive);

public sealed record PurchaseItemDto(
    Guid Id,
    Guid MedicineId,
    string MedicineName,
    string BatchNumber,
    DateOnly? ManufactureDate,
    DateOnly ExpiryDate,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal? SellingPrice,
    decimal LineTotal);

public sealed record PurchaseDto(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    DateOnly PurchaseDate,
    string Status,
    string? SupplierInvoiceNumber,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReceivedAtUtc,
    IReadOnlyList<PurchaseItemDto> Items);

public sealed record PurchasingSummaryDto(
    int DraftCount,
    int ReceivedThisMonthCount,
    decimal ReceivedThisMonthTotal);
