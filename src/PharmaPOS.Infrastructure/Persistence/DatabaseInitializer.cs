using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PharmaPOS.Infrastructure.Identity.Tenancy;

namespace PharmaPOS.Infrastructure.Persistence;

/// <summary>
/// Applies pending migrations and (optionally) seeds the minimum data the platform needs to be
/// usable locally. Kept out of the request pipeline and idempotent so it is safe to run on every
/// controlled startup.
/// </summary>
public static class DatabaseInitializer
{
    public const string DemoTenantId = "demo-pharmacy";

    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var options = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("PharmaPOS.Database");

        if (options.ApplyMigrationsOnStartup)
        {
            var store = provider.GetRequiredService<TenantStoreDbContext>();
            await store.Database.MigrateAsync(cancellationToken);

            var application = provider.GetRequiredService<AppDbContext>();
            await application.Database.MigrateAsync(cancellationToken);

            logger.LogInformation("Database migrations applied.");
        }

        if (!options.SeedDemoData)
        {
            return;
        }

        var tenantStore = provider.GetRequiredService<TenantStoreDbContext>();
        var exists = await tenantStore.TenantInfo
            .AnyAsync(tenant => tenant.Id == DemoTenantId, cancellationToken);

        if (exists)
        {
            return;
        }

        tenantStore.TenantInfo.Add(new AppTenantInfo
        {
            Id = DemoTenantId,
            Identifier = DemoTenantId,
            Name = "Demo Pharmacy",
            Status = "Active",
        });

        await tenantStore.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded demo tenant {TenantId}.", DemoTenantId);
    }
}
