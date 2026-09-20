namespace PharmaPOS.Domain.Catalog;

/// <summary>
/// Known dosage forms. Validated in the application layer; <c>Other</c> is the escape hatch so
/// unusual formulations never block catalog entry.
/// </summary>
public static class DosageForms
{
    public const string Tablet = "Tablet";
    public const string Capsule = "Capsule";
    public const string Syrup = "Syrup";
    public const string Suspension = "Suspension";
    public const string Injection = "Injection";
    public const string Cream = "Cream";
    public const string Ointment = "Ointment";
    public const string Drops = "Drops";
    public const string Inhaler = "Inhaler";
    public const string Powder = "Powder";
    public const string Spray = "Spray";
    public const string Suppository = "Suppository";
    public const string Other = "Other";

    public static IReadOnlyList<string> All { get; } =
    [
        Tablet, Capsule, Syrup, Suspension, Injection, Cream, Ointment, Drops, Inhaler,
        Powder, Spray, Suppository, Other,
    ];
}
