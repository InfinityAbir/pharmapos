using Finbuckle.MultiTenant.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PharmaPOS.Infrastructure.Identity.Tenancy;
using PharmaPOS.Infrastructure.Persistence;
using Respawn;
using Testcontainers.PostgreSql;
using Xunit;

namespace PharmaPOS.IntegrationTests;

/// <summary>
/// Starts one disposable PostgreSQL instance for the whole test class, applies migrations once,
/// and resets data between tests so isolation assertions are deterministic.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string MigrationTenant = "migration-tenant";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    private Respawner _respawner = default!;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using (var store = CreateStoreContext())
        {
            await store.Database.MigrateAsync();
        }

        await using (var app = CreateAppContext(MigrationTenant))
        {
            await app.Database.MigrateAsync();
        }

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
        });
    }

    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    public AppDbContext CreateAppContext(string tenantId)
    {
        var tenantInfo = new AppTenantInfo
        {
            Id = tenantId,
            Identifier = tenantId,
            Name = tenantId,
        };

        return MultiTenantDbContext.Create<AppDbContext, AppTenantInfo>(tenantInfo, BuildAppOptions());
    }

    public TenantStoreDbContext CreateStoreContext() =>
        new(new DbContextOptionsBuilder<TenantStoreDbContext>()
            .UseNpgsql(
                ConnectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(TenantStoreDbContext).Assembly.FullName))
            .Options);

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    private DbContextOptions<AppDbContext> BuildAppOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                ConnectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;
}
