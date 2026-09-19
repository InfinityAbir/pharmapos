namespace PharmaPOS.Infrastructure.Persistence;

/// <summary>Strongly-typed options for the database. The connection string lives under
/// <c>ConnectionStrings:Database</c> (idiomatic); the remaining switches live here.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// When true, pending EF Core migrations are applied at API startup. Intended for local
    /// development and controlled deployments only — production applies migrations explicitly.
    /// </summary>
    public bool ApplyMigrationsOnStartup { get; set; }

    /// <summary>
    /// When true, idempotently seeds the demo tenant used for local development and end-to-end
    /// checks. Never enable this in production.
    /// </summary>
    public bool SeedDemoData { get; set; }
}
