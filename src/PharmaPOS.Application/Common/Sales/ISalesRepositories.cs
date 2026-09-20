using PharmaPOS.Application.Features.Sales;

namespace PharmaPOS.Application.Common.Sales;

/// <summary>
/// Checkout: one atomic transaction per sale (sale + items + payments + batch deductions +
/// ledger + idempotency record). Prices resolve server-side from batches; the client sends
/// quantities, never money.
/// </summary>
public interface ICheckoutService
{
    Task<CheckoutResult> CheckoutAsync(
        CheckoutInput input,
        CancellationToken cancellationToken = default);
}

/// <summary>Outcome of a checkout: replays return the stored sale without side effects.</summary>
public sealed record CheckoutResult(SaleDto Sale, bool Replayed);

public sealed record CheckoutInput(
    IReadOnlyList<CheckoutItemInput> Items,
    IReadOnlyList<CheckoutPaymentInput> Payments,
    decimal DiscountAmount,
    string? CustomerName,
    string? CustomerPhone,
    string? PrescriptionReference,
    string IdempotencyKey);

public sealed record CheckoutItemInput(
    Guid MedicineId,
    int Quantity,
    Guid? BatchId,
    decimal DiscountAmount);

public sealed record CheckoutPaymentInput(
    string Method,
    decimal Amount,
    string? Reference);

/// <summary>Tenant-scoped sale history and receipts (reads only; sales are immutable).</summary>
public interface ISaleReader
{
    Task<IReadOnlyList<SaleDto>> ListAsync(
        DateOnly? from,
        DateOnly? to,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<SaleDto?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ReceiptDto?> GetReceiptAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TodaySalesDto> GetTodaySummaryAsync(CancellationToken cancellationToken = default);
}
