/**
 * Client-side mirrors of the server permission codes. The server is authoritative — these are
 * only used to decide which navigation entries and pages to show. Every request is re-checked
 * by the API against the caller's Keycloak realm roles.
 */
export const PERMISSIONS = {
  platformTenantsManage: 'platform.tenants.manage',
  platformUsersManage: 'platform.users.manage',
  platformAuditRead: 'platform.audit.read',
  tenantMembersManage: 'tenant.members.manage',
  tenantRolesManage: 'tenant.roles.manage',
  tenantMedicinesRead: 'tenant.medicines.read',
  tenantMedicinesManage: 'tenant.medicines.manage',
  tenantPharmacyManage: 'tenant.pharmacy.manage',
  tenantInventoryRead: 'tenant.inventory.read',
  tenantInventoryManage: 'tenant.inventory.manage',
  tenantPurchasingRead: 'tenant.purchasing.read',
  tenantPurchasingManage: 'tenant.purchasing.manage',
  tenantSalesRead: 'tenant.sales.read',
  tenantSalesManage: 'tenant.sales.manage',
  tenantCustomersRead: 'tenant.customers.read',
  tenantCustomersManage: 'tenant.customers.manage',
  tenantReturnsRead: 'tenant.returns.read',
  tenantReturnsManage: 'tenant.returns.manage',
} as const;

/** Keycloak realm roles (mirror of the server RealmRoles). */
export const ROLES = {
  platformAdmin: 'platform-admin',
  pharmacyOwner: 'pharmacy-owner',
  manager: 'manager',
  cashier: 'cashier',
  inventoryStaff: 'inventory-staff',
} as const;

/**
 * Which realm roles grant each permission (mirror of the server map). Used to derive page
 * visibility from the access token's roles without an extra round-trip.
 */
export const ROLE_PERMISSIONS: Readonly<Record<string, readonly string[]>> = {
  [PERMISSIONS.platformTenantsManage]: [ROLES.platformAdmin],
  [PERMISSIONS.platformUsersManage]: [ROLES.platformAdmin],
  [PERMISSIONS.platformAuditRead]: [ROLES.platformAdmin, ROLES.pharmacyOwner],
  [PERMISSIONS.tenantMembersManage]: [ROLES.platformAdmin, ROLES.pharmacyOwner],
  [PERMISSIONS.tenantRolesManage]: [ROLES.platformAdmin, ROLES.pharmacyOwner],
  [PERMISSIONS.tenantMedicinesRead]: [ROLES.platformAdmin, ROLES.pharmacyOwner, ROLES.manager, ROLES.cashier, ROLES.inventoryStaff],
  [PERMISSIONS.tenantMedicinesManage]: [ROLES.platformAdmin, ROLES.pharmacyOwner],
  [PERMISSIONS.tenantPharmacyManage]: [ROLES.platformAdmin, ROLES.pharmacyOwner],
  [PERMISSIONS.tenantInventoryRead]: [ROLES.platformAdmin, ROLES.pharmacyOwner, ROLES.manager, ROLES.inventoryStaff],
  [PERMISSIONS.tenantInventoryManage]: [ROLES.platformAdmin, ROLES.pharmacyOwner, ROLES.inventoryStaff],
  [PERMISSIONS.tenantPurchasingRead]: [ROLES.platformAdmin, ROLES.pharmacyOwner, ROLES.manager],
  [PERMISSIONS.tenantPurchasingManage]: [ROLES.platformAdmin, ROLES.pharmacyOwner],
  [PERMISSIONS.tenantSalesRead]: [ROLES.platformAdmin, ROLES.pharmacyOwner, ROLES.manager, ROLES.cashier],
  [PERMISSIONS.tenantSalesManage]: [ROLES.platformAdmin, ROLES.pharmacyOwner, ROLES.cashier],
  [PERMISSIONS.tenantCustomersRead]: [ROLES.platformAdmin, ROLES.pharmacyOwner, ROLES.manager, ROLES.cashier],
  [PERMISSIONS.tenantCustomersManage]: [ROLES.platformAdmin, ROLES.pharmacyOwner, ROLES.cashier],
  [PERMISSIONS.tenantReturnsRead]: [ROLES.platformAdmin, ROLES.pharmacyOwner, ROLES.manager, ROLES.cashier],
  [PERMISSIONS.tenantReturnsManage]: [ROLES.platformAdmin, ROLES.pharmacyOwner, ROLES.cashier],
};

/** Permission catalog shown on the Roles page (mirrors the server catalog). */
export interface PermissionDescriptor {
  readonly code: string;
  readonly module: string;
  readonly description: string;
}

export const PERMISSION_CATALOG: readonly PermissionDescriptor[] = [
  {
    code: PERMISSIONS.platformTenantsManage,
    module: 'platform',
    description: 'Manage tenants across the SaaS.',
  },
  {
    code: PERMISSIONS.platformUsersManage,
    module: 'platform',
    description: 'Manage platform users and identities.',
  },
  {
    code: PERMISSIONS.platformAuditRead,
    module: 'platform',
    description: 'Read the audit log.',
  },
  {
    code: PERMISSIONS.tenantMembersManage,
    module: 'tenant',
    description: "Manage the tenant's users and their roles.",
  },
  {
    code: PERMISSIONS.tenantRolesManage,
    module: 'tenant',
    description: "Manage the tenant's roles and their permissions.",
  },
  {
    code: PERMISSIONS.tenantMedicinesRead,
    module: 'tenant',
    description: 'Search and view the medicine catalog.',
  },
  {
    code: PERMISSIONS.tenantMedicinesManage,
    module: 'tenant',
    description: 'Manage medicines, categories and manufacturers.',
  },
  {
    code: PERMISSIONS.tenantPharmacyManage,
    module: 'tenant',
    description: 'Manage the pharmacy profile.',
  },
  {
    code: PERMISSIONS.tenantInventoryRead,
    module: 'tenant',
    description: 'View stock levels, batches and movements.',
  },
  {
    code: PERMISSIONS.tenantInventoryManage,
    module: 'tenant',
    description: 'Receive, adjust and expire stock.',
  },
  {
    code: PERMISSIONS.tenantPurchasingRead,
    module: 'tenant',
    description: 'View suppliers and purchases.',
  },
  {
    code: PERMISSIONS.tenantPurchasingManage,
    module: 'tenant',
    description: 'Manage suppliers and purchases.',
  },
  {
    code: PERMISSIONS.tenantSalesRead,
    module: 'tenant',
    description: 'View sales history and receipts.',
  },
  {
    code: PERMISSIONS.tenantSalesManage,
    module: 'tenant',
    description: 'Operate checkout and complete sales.',
  },
  {
    code: PERMISSIONS.tenantCustomersRead,
    module: 'tenant',
    description: 'View customers and their purchase history.',
  },
  {
    code: PERMISSIONS.tenantCustomersManage,
    module: 'tenant',
    description: 'Create and manage customers.',
  },
  {
    code: PERMISSIONS.tenantReturnsRead,
    module: 'tenant',
    description: 'View sale returns and refunds.',
  },
  {
    code: PERMISSIONS.tenantReturnsManage,
    module: 'tenant',
    description: 'Process sale returns and refunds.',
  },
];
