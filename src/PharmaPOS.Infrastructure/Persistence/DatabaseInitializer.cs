using Finbuckle.MultiTenant.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PharmaPOS.Application.Common.Authorization;
using PharmaPOS.Application.Features.Tenancy;
using PharmaPOS.Domain.Identity;
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

    public const string DemoAdminEmail = "admin@pharmapos.local";
    public const string DemoOwnerEmail = "owner@demo-pharmacy.local";

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
        var demo = await tenantStore.TenantInfo
            .FirstOrDefaultAsync(tenant => tenant.Id == DemoTenantId, cancellationToken);

        if (demo is null)
        {
            demo = new AppTenantInfo
            {
                Id = DemoTenantId,
                Identifier = DemoTenantId,
                Name = "Demo Pharmacy",
                Status = TenantStatus.Active,
            };

            tenantStore.TenantInfo.Add(demo);
            await tenantStore.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded demo tenant {TenantId}.", DemoTenantId);
        }

        var dbOptions = provider.GetRequiredService<DbContextOptions<AppDbContext>>();
        await using var db = MultiTenantDbContext.Create<AppDbContext, AppTenantInfo>(demo, dbOptions);

        await SeedPermissionsAsync(db, cancellationToken);
        await SeedDemoIdentityAsync(db, cancellationToken);

        logger.LogInformation("Seeded demo identity data for tenant {TenantId}.", DemoTenantId);
    }

    private static async Task SeedPermissionsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        foreach (var descriptor in Permissions.All)
        {
            var exists = await db.Permissions
                .AnyAsync(permission => permission.Code == descriptor.Code, cancellationToken);

            if (exists)
            {
                continue;
            }

            db.Permissions.Add(new Permission
            {
                Code = descriptor.Code,
                Module = descriptor.Module,
                Description = descriptor.Description,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDemoIdentityAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var admin = await EnsureUserAsync(db, DemoAdminEmail, "Platform Administrator", cancellationToken);
        var owner = await EnsureUserAsync(db, DemoOwnerEmail, "Demo Pharmacy Owner", cancellationToken);

        // V1 tenant roles. Manager/Cashier/Inventory Staff intentionally carry no permissions yet:
        // their modules land in later phases, and no fake permissions are granted.
        var platformAdmin = await EnsureRoleAsync(
            db, "Platform Admin", "Full platform and tenant administration.", isSystem: true,
            [Permissions.Platform.TenantsManage, Permissions.Platform.UsersManage,
             Permissions.Platform.AuditRead, Permissions.Tenant.MembersManage,
             Permissions.Tenant.RolesManage],
            cancellationToken);

        var ownerRole = await EnsureRoleAsync(
            db, "Owner", "Pharmacy owner: audit visibility and tenant administration.", isSystem: true,
            [Permissions.Platform.AuditRead, Permissions.Tenant.MembersManage,
             Permissions.Tenant.RolesManage],
            cancellationToken);

        await EnsureRoleAsync(db, "Manager", "Shift supervision (permissions arrive with later modules).",
            isSystem: true, [], cancellationToken);
        await EnsureRoleAsync(db, "Cashier", "Checkout operation (permissions arrive with the POS phase).",
            isSystem: true, [], cancellationToken);
        await EnsureRoleAsync(db, "Inventory Staff", "Stock handling (permissions arrive with the inventory phase).",
            isSystem: true, [], cancellationToken);

        await EnsureMembershipAsync(db, admin, platformAdmin, isDefault: true, cancellationToken);
        await EnsureMembershipAsync(db, owner, ownerRole, isDefault: true, cancellationToken);
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        AppDbContext db,
        string email,
        string displayName,
        CancellationToken cancellationToken)
    {
        var normalized = email.ToLowerInvariant();

        var user = await db.ApplicationUsers
            .FirstOrDefaultAsync(candidate => candidate.Email == normalized, cancellationToken);

        if (user is not null)
        {
            return user;
        }

        user = new ApplicationUser
        {
            // KeycloakSubject stays null until the identity is provisioned/linked in Keycloak;
            // resolution falls back to email == Keycloak username for the demo accounts.
            KeycloakSubject = null,
            Email = normalized,
            DisplayName = displayName,
            IsActive = true,
        };

        db.ApplicationUsers.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return user;
    }

    private static async Task<Role> EnsureRoleAsync(
        AppDbContext db,
        string name,
        string? description,
        bool isSystem,
        string[] permissionCodes,
        CancellationToken cancellationToken)
    {
        var role = await db.Roles
            .Include(candidate => candidate.RolePermissions)
            .FirstOrDefaultAsync(candidate => candidate.Name == name, cancellationToken);

        if (role is null)
        {
            role = new Role
            {
                // Assigned explicitly: the tenant-composite alternate key must be non-null on Add.
                TenantId = TenantScope.CurrentTenantId(db),
                Name = name,
                Description = description,
                IsSystem = isSystem,
            };

            db.Roles.Add(role);
            await db.SaveChangesAsync(cancellationToken);
        }

        var granted = role.RolePermissions
            .Select(rolePermission => rolePermission.PermissionId)
            .ToHashSet();

        var permissionsByCode = await db.Permissions
            .Where(permission => permissionCodes.Contains(permission.Code))
            .ToDictionaryAsync(permission => permission.Code, cancellationToken);

        foreach (var code in permissionCodes.Distinct())
        {
            if (!permissionsByCode.TryGetValue(code, out var permission)
                || !granted.Add(permission.Id))
            {
                continue;
            }

            db.RolePermissions.Add(new RolePermission
            {
                TenantId = role.TenantId,
                RoleId = role.Id,
                PermissionId = permission.Id,
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        return role;
    }

    private static async Task EnsureMembershipAsync(
        AppDbContext db,
        ApplicationUser user,
        Role role,
        bool isDefault,
        CancellationToken cancellationToken)
    {
        var membership = await db.TenantMemberships
            .Include(candidate => candidate.MembershipRoles)
            .FirstOrDefaultAsync(
                candidate => candidate.ApplicationUserId == user.Id,
                cancellationToken);

        if (membership is null)
        {
            membership = new TenantMembership
            {
                // Assigned explicitly: the tenant-composite alternate key must be non-null on Add.
                TenantId = TenantScope.CurrentTenantId(db),
                ApplicationUserId = user.Id,
                Status = MembershipStatus.Active,
                IsDefault = isDefault,
            };

            db.TenantMemberships.Add(membership);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (membership.MembershipRoles.Any(assignment => assignment.RoleId == role.Id))
        {
            return;
        }

        db.MembershipRoles.Add(new MembershipRole
        {
            TenantId = membership.TenantId,
            MembershipId = membership.Id,
            RoleId = role.Id,
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}