using PharmaPOS.Application.Features.Catalog;

namespace PharmaPOS.Application.Features.Sales;

public sealed record SaleItemDto(
    Guid Id,
    Guid MedicineId,
    string MedicineName,
    Guid BatchId,
    string BatchNumber,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal LineTotal);

public sealed record SalePaymentDto(
    Guid Id,
    string Method,
    decimal Amount,
    string? Reference);

public sealed record SaleDto(
    Guid Id,
    string InvoiceNumber,
    DateTimeOffset OccurredAtUtc,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal ChangeDue,
    string? CustomerName,
    string? CustomerPhone,
    string? PrescriptionReference,
    string? CashierSubject,
    IReadOnlyList<SaleItemDto> Items,
    IReadOnlyList<SalePaymentDto> Payments);

public sealed record ReceiptDto(
    PharmacyProfileDto Pharmacy,
    SaleDto Sale);

public sealed record TodaySalesDto(
    DateOnly Date,
    int SaleCount,
    decimal TotalAmount);
