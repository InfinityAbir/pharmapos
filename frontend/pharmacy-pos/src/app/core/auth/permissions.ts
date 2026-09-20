/**
 * Client-side mirrors of the server permission codes. The server is authoritative — these are
 * only used to decide which navigation entries and pages to show. Every request is re-checked
 * by the API against the database membership graph.
 */
export const PERMISSIONS = {
  platformTenantsManage: 'platform.tenants.manage',
  platformUsersManage: 'platform.users.manage',
  platformAuditRead: 'platform.audit.read',
  tenantMembersManage: 'tenant.members.manage',
  tenantRolesManage: 'tenant.roles.manage',
} as const;

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
];
