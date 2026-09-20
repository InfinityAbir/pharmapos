namespace PharmaPOS.Domain.Inventory;

/// <summary>
/// Movement types for the append-only inventory ledger. Phase 3 writes receipts, adjustments and
/// expiries; sales, purchase returns and sale returns arrive with their phases and reuse the
/// same ledger.
/// </summary>
public static class MovementTypes
{
    public const string OpeningStock = "OPENING_STOCK";
    public const string PurchaseReceived = "PURCHASE_RECEIVED";
    public const string AdjustmentIn = "ADJUSTMENT_IN";
    public const string AdjustmentOut = "ADJUSTMENT_OUT";
    public const string Expired = "EXPIRED";

    // Reserved for later phases; never written by Phase 3 code.
    public const string Sale = "SALE";
    public const string SaleReturn = "SALE_RETURN";
    public const string PurchaseReturn = "PURCHASE_RETURN";

    public static IReadOnlyList<string> All { get; } =
    [
        OpeningStock, PurchaseReceived, AdjustmentIn, AdjustmentOut, Expired,
        Sale, SaleReturn, PurchaseReturn,
    ];
}
