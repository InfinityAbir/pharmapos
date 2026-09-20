namespace PharmaPOS.Domain.Purchasing;

/// <summary>Purchase document lifecycle. Drafts are editable; received/cancelled are terminal.</summary>
public static class PurchaseStatus
{
    public const string Draft = "Draft";
    public const string Received = "Received";
    public const string Cancelled = "Cancelled";

    public static IReadOnlyList<string> All { get; } = [Draft, Received, Cancelled];
}
