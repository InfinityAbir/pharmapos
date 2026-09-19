/**
 * Pure helpers for reading claims out of an OIDC token without trusting their shape.
 * Kept free of Angular so they can be unit-tested directly.
 */
export type TokenClaims = Record<string, unknown>;

export function claimAsString(claims: TokenClaims, name: string): string | undefined {
  const value = claims[name];
  return typeof value === 'string' ? value : undefined;
}

export function claimAsStringArray(claims: TokenClaims, name: string): string[] {
  const value = claims[name];

  if (Array.isArray(value)) {
    return value.filter((item): item is string => typeof item === 'string');
  }

  return typeof value === 'string' ? [value] : [];
}
