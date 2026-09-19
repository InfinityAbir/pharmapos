using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PharmaPOS.Infrastructure.Identity.Tenancy;

namespace PharmaPOS.Infrastructure.Persistence.DesignTime;

/// <summary>Design-time factory for the tenant store context used by <c>dotnet ef</c>.</summary>
public sealed class TenantStoreDbContextFactory : IDesignTimeDbContextFactory<TenantStoreDbContext>
{
    public TenantStoreDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? "Host=localhost;Port=5432;Database=pharmapos;Username=pharmapos;Password=postgres";

        var options = new DbContextOptionsBuilder<TenantStoreDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(TenantStoreDbContext).Assembly.FullName))
            .Options;

        return new TenantStoreDbContext(options);
    }
}
