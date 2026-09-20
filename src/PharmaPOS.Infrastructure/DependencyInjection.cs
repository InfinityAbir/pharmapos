using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using Finbuckle.MultiTenant.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PharmaPOS.Application.Common.Auditing;
using PharmaPOS.Application.Common.Authorization;
using PharmaPOS.Application.Common.Tenancy;
using PharmaPOS.Application.Features.Auditing;
using PharmaPOS.Infrastructure.Auditing;
using PharmaPOS.Infrastructure.Identity;
using PharmaPOS.Infrastructure.Identity.Tenancy;
using PharmaPOS.Infrastructure.Persistence;

namespace PharmaPOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string is not configured. Set 'ConnectionStrings:Database'.");
        }

        services.AddHttpContextAccessor();

        // The tenant-aware application context. The current tenant is bound at construction.
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        // The store context is intentionally not tenant-filtered: it resolves tenants themselves.
        services.AddDbContext<TenantStoreDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(TenantStoreDbContext).Assembly.FullName)));

        services.AddMultiTenant<AppTenantInfo>()
            .WithEFCoreStore<TenantStoreDbContext, AppTenantInfo>()
            .WithStrategy<ClaimsTenantStrategy>(ServiceLifetime.Singleton);

        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<IAuditLogReader, AuditLogReader>();

        services.AddScoped<IMembershipResolver, MembershipResolver>();
        services.AddScoped<IPlatformTenantStore, PlatformTenantStore>();
        services.AddScoped<ITenantRoleRepository, TenantRoleRepository>();
        services.AddScoped<ITenantMembershipRepository, TenantMembershipRepository>();

        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateOnStart();

        return services;
    }
}
