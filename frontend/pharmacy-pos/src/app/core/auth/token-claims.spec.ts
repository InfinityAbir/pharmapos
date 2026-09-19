import { claimAsString, claimAsStringArray, TokenClaims } from './token-claims';

describe('token claim helpers', () => {
  const claims: TokenClaims = {
    preferred_username: 'admin@pharmapos.local',
    tenant_id: 'demo-pharmacy',
    permissions: ['platform.audit.read', 'platform.users.manage'],
    single: 'one',
    mixed: ['ok', 42, null],
  };

  it('reads string claims', () => {
    expect(claimAsString(claims, 'tenant_id')).toBe('demo-pharmacy');
  });

  it('returns undefined for missing or non-string claims', () => {
    expect(claimAsString(claims, 'absent')).toBeUndefined();
    expect(claimAsString(claims, 'permissions')).toBeUndefined();
  });

  it('reads multi-valued string claims', () => {
    expect(claimAsStringArray(claims, 'permissions')).toEqual([
      'platform.audit.read',
      'platform.users.manage',
    ]);
  });

  it('normalises a single string claim into an array', () => {
    expect(claimAsStringArray(claims, 'single')).toEqual(['one']);
  });

  it('drops non-string entries from mixed arrays', () => {
    expect(claimAsStringArray(claims, 'mixed')).toEqual(['ok']);
  });

  it('returns an empty array for absent claims', () => {
    expect(claimAsStringArray(claims, 'absent')).toEqual([]);
  });
});
