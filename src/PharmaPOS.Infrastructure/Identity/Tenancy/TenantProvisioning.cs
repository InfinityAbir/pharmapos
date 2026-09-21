using Finbuckle.MultiTenant.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PharmaPOS.Application.Common.Authorization;
using PharmaPOS.Application.Common.Tenancy;
using PharmaPOS.Domain.Exceptions;
using PharmaPOS.Domain.Identity;
using PharmaPOS.Infrastructure.Identity.Tenancy;
using PharmaPOS.Infrastructure.Persistence;

namespace PharmaPOS.Infrastructure.Identity.Tenancy;

/// <summary>
/// Binds a fresh <see cref="AppDbContext"/> to the new tenant and seeds exactly what a tenant
/// needs to be operable: the permission catalog, an Owner role and the owner's membership.
/// Mirrors <c>DatabaseInitializer</c>'s demo seeding so new tenants start from the same baseline.
/// </summary>
public sealed class TenantProvisioning : ITenantProvisioning
{
    private readonly IServiceProvider _services;
    private readonly ILogger<TenantProvisioning> _logger;

    public TenantProvisioning(IServiceProvider services, ILogger<TenantProvisioning> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task ProvisionOwnerAsync(
        string tenantIdentifier,
        string email,
        string displayName,
        string? keycloakSubject,
        CancellationToken cancellationToken = default)
    {
        using var scope = _services.CreateScope();
        var provider = scope.ServiceProvider;

        var store = provider.GetRequiredService<TenantStoreDbContext>();
        var tenant = await store.TenantInfo
            .FirstOrDefaultAsync(candidate => candidate.Identifier == tenantIdentifier, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant '{tenantIdentifier}' does not exist.");

        var dbOptions = provider.GetRequiredService<DbContextOptions<AppDbContext>>();
        await using var db = MultiTenantDbContext.Create<AppDbContext, AppTenantInfo>(tenant, dbOptions);

        var normalized = email.Trim().ToLowerInvariant();

        var existing = await db.ApplicationUsers
            .FirstOrDefaultAsync(candidate => candidate.Email == normalized, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException(
                "auth.email_taken", "An account with this email already exists.");
        }

        var user = new ApplicationUser
        {
            KeycloakSubject = keycloakSubject,
            Email = normalized,
            DisplayName = displayName,
            IsActive = true,
        };
        db.ApplicationUsers.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        await EnsurePermissionsAsync(db, cancellationToken);
        var ownerRole = await EnsureOwnerRoleAsync(db, cancellationToken);

        var tenantId = TenantScope.CurrentTenantId(db);
        var membership = new TenantMembership
        {
            TenantId = tenantId,
            ApplicationUserId = user.Id,
            Status = MembershipStatus.Active,
            IsDefault = true,
        };
        db.TenantMemberships.Add(membership);
        await db.SaveChangesAsync(cancellationToken);

        db.MembershipRoles.Add(new MembershipRole
        {
            TenantId = tenantId,
            MembershipId = membership.Id,
            RoleId = ownerRole.Id,
        });
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Provisioned owner {Email} for tenant {Tenant}.", normalized, tenantIdentifier);
    }

    private static async Task EnsurePermissionsAsync(AppDbContext db, CancellationToken cancellationToken)
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

    private static async Task<Role> EnsureOwnerRoleAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var tenantId = TenantScope.CurrentTenantId(db);

        var role = await db.Roles
            .Include(candidate => candidate.RolePermissions)
            .FirstOrDefaultAsync(candidate => candidate.Name == "Owner", cancellationToken);

        // Same grants the demo seed gives Owner: full tenant administration.
        var ownerPermissions = new[]
        {
            Permissions.Platform.AuditRead, Permissions.Tenant.MembersManage,
            Permissions.Tenant.RolesManage, Permissions.Tenant.MedicinesRead,
            Permissions.Tenant.MedicinesManage, Permissions.Tenant.PharmacyManage,
            Permissions.Tenant.InventoryRead, Permissions.Tenant.InventoryManage,
            Permissions.Tenant.PurchasingRead, Permissions.Tenant.PurchasingManage,
            Permissions.Tenant.SalesRead, Permissions.Tenant.SalesManage,
            Permissions.Tenant.CustomersRead, Permissions.Tenant.CustomersManage,
            Permissions.Tenant.ReturnsRead, Permissions.Tenant.ReturnsManage,
        };

        if (role is null)
        {
            role = new Role
            {
                TenantId = tenantId,
                Name = "Owner",
                Description = "Pharmacy owner: audit visibility and tenant administration.",
                IsSystem = true,
            };
            db.Roles.Add(role);
            await db.SaveChangesAsync(cancellationToken);
        }

        var granted = role.RolePermissions
            .Select(rolePermission => rolePermission.PermissionId)
            .ToHashSet();

        var permissionsByCode = await db.Permissions
            .Where(permission => ownerPermissions.Contains(permission.Code))
            .ToDictionaryAsync(permission => permission.Code, cancellationToken);

        foreach (var code in ownerPermissions.Distinct())
        {
            if (!permissionsByCode.TryGetValue(code, out var permission) || !granted.Add(permission.Id))
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
}
