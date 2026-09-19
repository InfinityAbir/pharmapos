import { Injectable, computed, inject, signal } from '@angular/core';
import Keycloak from 'keycloak-js';
import { AppConfigService } from '../config/app-config.service';
import { claimAsString, claimAsStringArray, TokenClaims } from './token-claims';

/**
 * Owns the OIDC session against Keycloak.
 *
 * The SPA is a public client using Authorization Code flow with PKCE (S256) — no client secret is
 * ever shipped to the browser. Token issuance, refresh rotation and reuse detection all happen in
 * Keycloak; this service only carries the resulting access token and exposes the claims the API
 * itself re-validates on every request.
 */
@Injectable({ providedIn: 'root' })
export class KeycloakService {
  private readonly appConfig = inject(AppConfigService);

  private keycloak: Keycloak | undefined;
  private readonly claims = signal<TokenClaims>({});
  private readonly authenticated = signal(false);

  readonly isAuthenticated = this.authenticated.asReadonly();

  readonly username = computed(() => claimAsString(this.claims(), 'preferred_username'));
  readonly email = computed(() => claimAsString(this.claims(), 'email'));
  readonly tenantId = computed(() => claimAsString(this.claims(), 'tenant_id'));
  readonly permissions = computed(() => claimAsStringArray(this.claims(), 'permissions'));

  async init(): Promise<void> {
    const oidc = this.appConfig.value.oidc;

    const keycloak = new Keycloak({
      url: oidc.url,
      realm: oidc.realm,
      clientId: oidc.clientId,
    });
    this.keycloak = keycloak;

    keycloak.onTokenExpired = () => {
      void keycloak.updateToken(30).catch(() => this.login());
    };
    keycloak.onAuthLogout = () => this.setSession(false, {});

    const isAuthenticated = await keycloak.init({
      onLoad: 'check-sso',
      flow: 'standard',
      pkceMethod: 'S256',
      checkLoginIframe: false,
      responseMode: 'query',
    });

    this.setSession(isAuthenticated, (keycloak.tokenParsed as TokenClaims) ?? {});
  }

  ensureValidToken(): Promise<string | undefined> {
    const keycloak = this.keycloak;
    if (!keycloak || !keycloak.authenticated) {
      return Promise.resolve(undefined);
    }

    if (!keycloak.isTokenExpired(30)) {
      return Promise.resolve(keycloak.token);
    }

    return keycloak
      .updateToken(30)
      .then(() => keycloak.token)
      .catch(() => {
        this.setSession(false, {});
        return undefined;
      });
  }

  async login(): Promise<void> {
    await this.keycloak?.login({ redirectUri: `${window.location.origin}/dashboard` });
  }

  async logout(): Promise<void> {
    await this.keycloak?.logout({ redirectUri: window.location.origin });
  }

  hasPermission(permission: string): boolean {
    return this.permissions().includes(permission);
  }

  private setSession(isAuthenticated: boolean, claims: TokenClaims): void {
    this.authenticated.set(isAuthenticated);
    this.claims.set(claims);
  }
}
