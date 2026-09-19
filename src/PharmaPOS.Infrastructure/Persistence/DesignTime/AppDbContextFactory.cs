using Finbuckle.MultiTenant.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PharmaPOS.Infrastructure.Identity.Tenancy;

namespace PharmaPOS.Infrastructure.Persistence.DesignTime;

/// <summary>
/// Design-time factory for <see cref="AppDbContext"/> so <c>dotnet ef</c> can build the model
/// without a live tenant. Migrations are generated in this assembly and applied by the API host.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? "Host=localhost;Port=5432;Database=pharmapos;Username=pharmapos;Password=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;

        // A placeholder tenant is sufficient to build the model for migrations.
        var tenantInfo = new AppTenantInfo
        {
            Id = "design-time",
            Identifier = "design-time",
            Name = "Design-time tenant",
        };

        return MultiTenantDbContext.Create<AppDbContext, AppTenantInfo>(tenantInfo, options);
    }
}
