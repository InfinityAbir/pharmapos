import { describe, expect, it } from 'vitest';
import { PERMISSIONS, ROLES, ROLE_PERMISSIONS } from './permissions';

describe('realm role map', () => {
  it('grants every permission to at least one role', () => {
    for (const code of Object.values(PERMISSIONS)) {
      expect(ROLE_PERMISSIONS[code]?.length ?? 0, code).toBeGreaterThan(0);
    }
  });

  it('names only known realm roles', () => {
    const known: ReadonlySet<string> = new Set(Object.values(ROLES));
    for (const roles of Object.values(ROLE_PERMISSIONS)) {
      for (const role of roles) {
        expect(known.has(role), role).toBe(true);
      }
    }
  });

  it('gives cashiers checkout but not tenant administration', () => {
    const cashier = Object.entries(ROLE_PERMISSIONS)
      .filter(([, roles]) => roles.includes(ROLES.cashier))
      .map(([code]) => code);

    expect(cashier).toContain(PERMISSIONS.tenantSalesManage);
    expect(cashier).not.toContain(PERMISSIONS.tenantMembersManage);
    expect(cashier).not.toContain(PERMISSIONS.platformTenantsManage);
  });
});
