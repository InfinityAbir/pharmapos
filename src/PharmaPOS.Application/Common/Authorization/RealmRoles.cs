namespace PharmaPOS.Application.Common.Authorization;

/// <summary>
/// Keycloak realm roles — the single source of authorization truth, mirroring the realm roles
/// defined in <c>deploy/keycloak/realm-export.json</c>.
///
/// What a caller may do is decided from the <c>realm_access</c> roles inside their validated
/// access token (mapped to role claims at authentication time). The database membership graph
/// still records who belongs to which tenant (Team page, session context), but it is no longer
/// consulted for access decisions.
///
/// <see cref="RolePermissions"/> is derived from the same grants the database seeds grant each
/// system role, so switching models does not silently widen or narrow anyone's access.
/// </summary>
public static class RealmRoles
{
    public const string PlatformAdmin = "platform-admin";
    public const string PharmacyOwner = "pharmacy-owner";
    public const string Manager = "manager";
    public const string Cashier = "cashier";
    public const string InventoryStaff = "inventory-staff";

    public static IReadOnlyList<string> All { get; } =
    [
        PlatformAdmin,
        PharmacyOwner,
        Manager,
        Cashier,
        InventoryStaff,
    ];

    /// <summary>Which realm roles grant a permission. PlatformAdmin holds every permission.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> RolePermissions { get; } =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [Permissions.Platform.TenantsManage] = [PlatformAdmin],
            [Permissions.Platform.UsersManage] = [PlatformAdmin],
            [Permissions.Platform.AuditRead] = [PlatformAdmin, PharmacyOwner],
            [Permissions.Tenant.MembersManage] = [PlatformAdmin, PharmacyOwner],
            [Permissions.Tenant.RolesManage] = [PlatformAdmin, PharmacyOwner],
            [Permissions.Tenant.MedicinesRead] = [PlatformAdmin, PharmacyOwner, Manager, Cashier, InventoryStaff],
            [Permissions.Tenant.MedicinesManage] = [PlatformAdmin, PharmacyOwner],
            [Permissions.Tenant.PharmacyManage] = [PlatformAdmin, PharmacyOwner],
            [Permissions.Tenant.InventoryRead] = [PlatformAdmin, PharmacyOwner, Manager, InventoryStaff],
            [Permissions.Tenant.InventoryManage] = [PlatformAdmin, PharmacyOwner, InventoryStaff],
            [Permissions.Tenant.PurchasingRead] = [PlatformAdmin, PharmacyOwner, Manager],
            [Permissions.Tenant.PurchasingManage] = [PlatformAdmin, PharmacyOwner],
            [Permissions.Tenant.SalesRead] = [PlatformAdmin, PharmacyOwner, Manager, Cashier],
            [Permissions.Tenant.SalesManage] = [PlatformAdmin, PharmacyOwner, Cashier],
            [Permissions.Tenant.CustomersRead] = [PlatformAdmin, PharmacyOwner, Manager, Cashier],
            [Permissions.Tenant.CustomersManage] = [PlatformAdmin, PharmacyOwner, Cashier],
            [Permissions.Tenant.ReturnsRead] = [PlatformAdmin, PharmacyOwner, Manager, Cashier],
            [Permissions.Tenant.ReturnsManage] = [PlatformAdmin, PharmacyOwner, Cashier],
        };

    /// <summary>All permissions granted by a set of realm roles.</summary>
    public static IReadOnlyList<string> PermissionsFor(IEnumerable<string> roles)
    {
        var granted = new HashSet<string>();
        foreach (var role in roles)
        {
            foreach (var (permission, holders) in RolePermissions)
            {
                if (holders.Contains(role))
                {
                    granted.Add(permission);
                }
            }
        }

        return granted.OrderBy(code => code).ToArray();
    }
}
