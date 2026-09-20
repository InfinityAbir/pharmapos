namespace PharmaPOS.Application.Common.Authorization;

/// <summary>Descriptor for the platform permission catalog (code, module, description).</summary>
public sealed record PermissionDescriptor(string Code, string Module, string? Description);

/// <summary>
/// The permission catalog. Codes are the contract between the server (which enforces them) and the
/// database (which stores them in the <c>permissions</c> table and grants them to tenant roles).
/// Codes are never trusted from the client; the API resolves the caller's permissions server-side
/// from their tenant membership.
/// </summary>
public static class Permissions
{
    public static class Platform
    {
        public const string TenantsManage = "platform.tenants.manage";
        public const string UsersManage = "platform.users.manage";
        public const string AuditRead = "platform.audit.read";
    }

    public static class Tenant
    {
        public const string MembersManage = "tenant.members.manage";
        public const string RolesManage = "tenant.roles.manage";
        public const string MedicinesRead = "tenant.medicines.read";
        public const string MedicinesManage = "tenant.medicines.manage";
        public const string PharmacyManage = "tenant.pharmacy.manage";
        public const string InventoryRead = "tenant.inventory.read";
        public const string InventoryManage = "tenant.inventory.manage";
        public const string PurchasingRead = "tenant.purchasing.read";
        public const string PurchasingManage = "tenant.purchasing.manage";
        public const string SalesRead = "tenant.sales.read";
        public const string SalesManage = "tenant.sales.manage";
        public const string CustomersRead = "tenant.customers.read";
        public const string CustomersManage = "tenant.customers.manage";
        public const string ReturnsRead = "tenant.returns.read";
        public const string ReturnsManage = "tenant.returns.manage";
    }

    /// <summary>Complete catalog, used for seeding and for validating role definitions.</summary>
    public static IReadOnlyList<PermissionDescriptor> All { get; } =
    [
        new(Permissions.Platform.TenantsManage, "platform", "Manage tenants across the SaaS."),
        new(Permissions.Platform.UsersManage, "platform", "Manage platform users and identities."),
        new(Permissions.Platform.AuditRead, "platform", "Read the audit log."),
        new(Permissions.Tenant.MembersManage, "tenant", "Manage the tenant's users and their roles."),
        new(Permissions.Tenant.RolesManage, "tenant", "Manage the tenant's roles and their permissions."),
        new(Permissions.Tenant.MedicinesRead, "tenant", "Search and view the medicine catalog."),
        new(Permissions.Tenant.MedicinesManage, "tenant", "Manage medicines, categories and manufacturers."),
        new(Permissions.Tenant.PharmacyManage, "tenant", "Manage the pharmacy profile."),
        new(Permissions.Tenant.InventoryRead, "tenant", "View stock levels, batches and movements."),
        new(Permissions.Tenant.InventoryManage, "tenant", "Receive, adjust and expire stock."),
        new(Permissions.Tenant.PurchasingRead, "tenant", "View suppliers and purchases."),
        new(Permissions.Tenant.PurchasingManage, "tenant", "Manage suppliers and purchases."),
        new(Permissions.Tenant.SalesRead, "tenant", "View sales history and receipts."),
        new(Permissions.Tenant.SalesManage, "tenant", "Operate checkout and complete sales."),
        new(Permissions.Tenant.CustomersRead, "tenant", "View customers and their purchase history."),
        new(Permissions.Tenant.CustomersManage, "tenant", "Create and manage customers."),
        new(Permissions.Tenant.ReturnsRead, "tenant", "View sale returns and refunds."),
        new(Permissions.Tenant.ReturnsManage, "tenant", "Process sale returns and refunds."),
    ];

    /// <summary>Whether a permission code exists in the catalog.</summary>
    public static bool Contains(string code) => All.Any(descriptor => descriptor.Code == code);
}