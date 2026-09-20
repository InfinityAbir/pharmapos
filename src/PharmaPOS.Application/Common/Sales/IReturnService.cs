using PharmaPOS.Application.Features.Returns;

namespace PharmaPOS.Application.Common.Sales;

/// <summary>
/// Sale returns: one atomic transaction per return (return + items + batch restock +
/// SALE_RETURN movements + idempotency record). Refund money is computed server-side from
/// the original line nets; the client sends quantities, never money.
/// </summary>
public interface IReturnService
{
    Task<ProcessReturnResult> ProcessAsync(
        ReturnInput input,
        CancellationToken cancellationToken = default);
}

/// <summary>Outcome of a return: replays return the stored return without side effects.</summary>
public sealed record ProcessReturnResult(ReturnDto Return, bool Replayed);

public sealed record ReturnInput(
    Guid SaleId,
    IReadOnlyList<ReturnItemInput> Items,
    string Reason,
    bool Restock,
    string RefundMethod,
    string? RefundReference,
    string IdempotencyKey);

public sealed record ReturnItemInput(
    Guid SaleItemId,
    int Quantity);

/// <summary>Tenant-scoped return history and returnable-quantity reads.</summary>
public interface IReturnReader
{
    Task<IReadOnlyList<ReturnDto>> ListAsync(
        Guid? saleId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<ReturnDto?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ReturnableSaleDto?> GetReturnableAsync(
        Guid saleId,
        CancellationToken cancellationToken = default);
}
