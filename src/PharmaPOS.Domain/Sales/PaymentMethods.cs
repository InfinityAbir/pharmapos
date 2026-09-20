namespace PharmaPOS.Domain.Sales;

/// <summary>V1 payment methods (manual recording; no gateway claims).</summary>
public static class PaymentMethods
{
    public const string Cash = "Cash";
    public const string Card = "Card";
    public const string BKash = "bKash";
    public const string Nagad = "Nagad";
    public const string Other = "Other";

    public static IReadOnlyList<string> All { get; } = [Cash, Card, BKash, Nagad, Other];

    /// <summary>Methods that must carry a transaction reference.</summary>
    public static bool RequiresReference(string method) => method != Cash;
}
