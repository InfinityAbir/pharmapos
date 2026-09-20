using PharmaPOS.Application.Features.Purchasing;
using PharmaPOS.Domain.Exceptions;

namespace PharmaPOS.Application.Common.Purchasing;

/// <summary>Server-side purchase totals. The client never sends money totals.</summary>
public static class PurchaseTotals
{
    public static (decimal Subtotal, decimal Total) Calculate(
        IReadOnlyList<PurchaseItemInput> items,
        decimal headerDiscount,
        decimal tax)
    {
        if (headerDiscount < 0)
        {
            throw new BusinessRuleException(
                "purchase.discount_invalid",
                "Purchase discount cannot be negative.");
        }

        if (tax < 0)
        {
            throw new BusinessRuleException(
                "purchase.tax_invalid",
                "Purchase tax cannot be negative.");
        }

        decimal subtotal = 0;
        foreach (var item in items)
        {
            var gross = item.Quantity * item.UnitPrice;
            if (item.DiscountAmount < 0 || item.DiscountAmount > gross)
            {
                throw new BusinessRuleException(
                    "purchase.item_discount_exceeded",
                    $"Item discount {item.DiscountAmount:F4} exceeds its line gross {gross:F4}.");
            }

            subtotal += gross - item.DiscountAmount;
        }

        if (headerDiscount > subtotal)
        {
            throw new BusinessRuleException(
                "purchase.discount_exceeded",
                $"Purchase discount {headerDiscount:F4} exceeds the subtotal {subtotal:F4}.");
        }

        return (subtotal, subtotal - headerDiscount + tax);
    }

    public static decimal LineTotal(PurchaseItemInput item)
        => item.Quantity * item.UnitPrice - item.DiscountAmount;
}

/// <summary>Tenant-scoped suppliers.</summary>
public interface ISupplierRepository
{
    Task<IReadOnlyList<SupplierDto>> ListAsync(
        string? search,
        CancellationToken cancellationToken = default);

    Task<SupplierDto?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SupplierDto> CreateAsync(
        SupplierInput input,
        CancellationToken cancellationToken = default);

    Task<SupplierDto> UpdateAsync(
        Guid id,
        SupplierInput input,
        CancellationToken cancellationToken = default);

    Task<SupplierDto> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record SupplierInput(
    string Name,
    string? Company,
    string? Phone,
    string? Email,
    string? Address,
    string? ContactPerson,
    string? PaymentTerms);

/// <summary>Tenant-scoped purchase documents with atomic idempotent receiving.</summary>
public interface IPurchaseRepository
{
    Task<IReadOnlyList<PurchaseDto>> ListAsync(
        Guid? supplierId,
        string? status,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<PurchaseDto?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PurchaseDto> CreateDraftAsync(
        PurchaseInput input,
        CancellationToken cancellationToken = default);

    Task<PurchaseDto> UpdateDraftAsync(
        Guid id,
        PurchaseInput input,
        CancellationToken cancellationToken = default);

    Task<ReceivePurchaseResult> ReceiveAsync(
        Guid id,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<PurchaseDto> CancelAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PurchasingSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}

public sealed record PurchaseInput(
    Guid SupplierId,
    DateOnly PurchaseDate,
    string? SupplierInvoiceNumber,
    decimal DiscountAmount,
    decimal TaxAmount,
    string? Notes,
    IReadOnlyList<PurchaseItemInput> Items);

/// <summary>Outcome of a receive: replays return the stored purchase without side effects.</summary>
public sealed record ReceivePurchaseResult(PurchaseDto Purchase, bool Replayed);

public sealed record PurchaseItemInput(
    Guid MedicineId,
    string BatchNumber,
    DateOnly? ManufactureDate,
    DateOnly ExpiryDate,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal? SellingPrice);
