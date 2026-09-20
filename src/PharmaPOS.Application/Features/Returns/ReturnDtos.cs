namespace PharmaPOS.Application.Features.Returns;

public sealed record ReturnItemDto(
    Guid Id,
    Guid SaleItemId,
    Guid MedicineId,
    string MedicineName,
    Guid BatchId,
    string BatchNumber,
    int Quantity,
    decimal UnitPrice,
    decimal RefundAmount);

public sealed record ReturnDto(
    Guid Id,
    Guid SaleId,
    string InvoiceNumber,
    string Reason,
    bool Restock,
    string RefundMethod,
    string? RefundReference,
    decimal RefundAmount,
    DateTimeOffset OccurredAtUtc,
    string? ActorSubject,
    IReadOnlyList<ReturnItemDto> Items);

public sealed record ReturnableItemDto(
    Guid SaleItemId,
    Guid MedicineId,
    string MedicineName,
    Guid BatchId,
    string BatchNumber,
    int SoldQuantity,
    int AlreadyReturnedQuantity,
    int ReturnableQuantity,
    decimal UnitPrice,
    decimal NetPerUnit);

public sealed record ReturnableSaleDto(
    Guid SaleId,
    string InvoiceNumber,
    decimal TotalAmount,
    decimal AlreadyRefundedAmount,
    IReadOnlyList<ReturnableItemDto> Items);
