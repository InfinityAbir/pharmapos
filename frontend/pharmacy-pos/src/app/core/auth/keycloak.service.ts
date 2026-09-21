import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AuthApi, RegisterPayload } from '../http/auth-api.service';
import { ROLE_PERMISSIONS, ROLES } from './permissions';
import { TokenClaims, claimAsString, claimAsStringArray } from './token-claims';

interface StoredTokens {
  readonly accessToken: string;
  readonly refreshToken: string | null;
  readonly expiresAt: number;
}

const STORAGE_KEY = 'pharmapos.tokens';

/**
 * Owns the app session against the backend auth endpoints (SylviaNG-style).
 *
 * The SPA posts credentials to the API, which proxies Keycloak server-side and returns tokens.
 * Tokens persist in local storage so a reload keeps the session; the access token is refreshed
 * through the API when close to expiry. Roles come from the access token's realm roles and gate
 * navigation/pages only — the API re-authorizes every request.
 */
@Injectable({ providedIn: 'root' })
export class KeycloakService {
  private readonly authApi = inject(AuthApi);

  private readonly claims = signal<TokenClaims>({});
  private readonly authenticated = signal(false);
  private refreshInFlight: Promise<string | undefined> | undefined;

  readonly isAuthenticated = this.authenticated.asReadonly();

  readonly username = computed(() => claimAsString(this.claims(), 'preferred_username'));
  readonly email = computed(() => claimAsString(this.claims(), 'email'));
  readonly tenantId = computed(() => claimAsString(this.claims(), 'tenant_id'));
  readonly roles = computed(() => readRoles(this.claims()));
  readonly permissions = computed(() => permissionsFor(this.roles()));

  /** Restores a persisted session. Called once at startup after config loads. */
  async init(): Promise<void> {
    const stored = readStored();
    if (!stored) {
      this.setSession(false, {});
      return;
    }

    this.applyTokens(stored.accessToken);
    if (!this.authenticated()) {
      clearStored();
    }
  }

  async login(email: string, password: string): Promise<void> {
    const response = await firstValueFrom(this.authApi.login(email, password));
    this.store(response.accessToken, response.refreshToken, response.expiresInSeconds);
  }

  async register(payload: RegisterPayload): Promise<void> {
    const response = await firstValueFrom(this.authApi.register(payload));
    this.store(response.accessToken, response.refreshToken, response.expiresInSeconds);
  }

  logout(): void {
    clearStored();
    this.refreshInFlight = undefined;
    this.setSession(false, {});
  }

  /** Current access token, refreshing first when close to expiry. */
  ensureValidToken(): Promise<string | undefined> {
    const stored = readStored();
    if (!stored) {
      return Promise.resolve(undefined);
    }

    if (stored.expiresAt - Date.now() > 30_000) {
      return Promise.resolve(stored.accessToken);
    }

    if (!stored.refreshToken) {
      this.logout();
      return Promise.resolve(undefined);
    }

    this.refreshInFlight ??= firstValueFrom(this.authApi.refresh(stored.refreshToken))
      .then((response) => {
        this.store(response.accessToken, response.refreshToken, response.expiresInSeconds);
        return this.readAccess();
      })
      .catch(() => {
        this.logout();
        return undefined;
      })
      .finally(() => {
        this.refreshInFlight = undefined;
      });

    return this.refreshInFlight;
  }

  hasPermission(permission: string): boolean {
    return this.permissions().includes(permission);
  }

  hasRole(role: string): boolean {
    return this.roles().includes(role);
  }

  private store(accessToken: string, refreshToken: string | null, expiresInSeconds: number): void {
    const stored: StoredTokens = {
      accessToken,
      refreshToken,
      expiresAt: Date.now() + Math.max(0, expiresInSeconds - 10) * 1000,
    };
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(stored));
    } catch {
      // Private-mode storage failures must never break the UI.
    }
    this.applyTokens(accessToken);
  }

  private readAccess(): string | undefined {
    return readStored()?.accessToken;
  }

  private applyTokens(accessToken: string): void {
    const claims = decodePayload(accessToken);
    const username = claimAsString(claims, 'preferred_username') ?? claimAsString(claims, 'email');
    this.setSession(!!username, claims);
  }

  private setSession(isAuthenticated: boolean, claims: TokenClaims): void {
    this.authenticated.set(isAuthenticated);
    this.claims.set(claims);
  }
}

function readStored(): StoredTokens | undefined {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return undefined;
    }
    const parsed = JSON.parse(raw) as StoredTokens;
    return typeof parsed.accessToken === 'string' ? parsed : undefined;
  } catch {
    return undefined;
  }
}

function clearStored(): void {
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch {
    // Storage unavailable: nothing to clear.
  }
}

/** Reads realm roles from either the standard realm_access blob or a roles array claim. */
function readRoles(claims: TokenClaims): string[] {
  const direct = claimAsStringArray(claims, 'roles');
  const access = claims['realm_access'];
  if (access && typeof access === 'object') {
    const nested = claimAsStringArray(access as TokenClaims, 'roles');
    return [...new Set([...direct, ...nested])];
  }
  return [...new Set(direct)];
}

function permissionsFor(roles: readonly string[]): string[] {
  const granted = new Set<string>();
  for (const [permission, holders] of Object.entries(ROLE_PERMISSIONS)) {
    if (holders.some((role) => roles.includes(role))) {
      granted.add(permission);
    }
  }
  return [...granted].sort();
}

/** Decodes a JWT payload without verifying (the API verifies signatures). */
function decodePayload(token: string): TokenClaims {
  try {
    const segment = token.split('.')[1];
    const normalized = segment.replace(/-/g, '+').replace(/_/g, '/');
    return JSON.parse(atob(normalized)) as TokenClaims;
  } catch {
    return {};
  }
}

export { ROLES };
