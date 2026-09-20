import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { KeycloakService } from '../auth/keycloak.service';
import { CurrentMembership, PlatformApi } from '../http/platform-api.service';

/**
 * Holds the server-resolved session context for the shell and pages.
 *
 * Loaded once per sign-in from GET /platform/whoami: who the caller is, the active tenant, and
 * the roles/permissions the database grants them there. `hasPermission` gates navigation and
 * page affordances only — the API re-authorizes every request, and a caller without an active
 * membership resolves to `forbidden` here (the API answers 403).
 */
@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly api = inject(PlatformApi);
  private readonly keycloak = inject(KeycloakService);

  readonly context = signal<CurrentMembership | undefined>(undefined);
  readonly loaded = signal(false);
  readonly forbidden = signal(false);
  readonly error = signal<string | undefined>(undefined);

  async refresh(): Promise<void> {
    this.loaded.set(false);
    this.forbidden.set(false);
    this.error.set(undefined);

    if (!this.keycloak.isAuthenticated()) {
      this.context.set(undefined);
      this.loaded.set(true);
      return;
    }

    try {
      this.context.set(await firstValueFrom(this.api.whoami()));
    } catch (error) {
      this.context.set(undefined);
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.forbidden.set(true);
      } else {
        this.error.set('Your session could not be loaded.');
      }
    } finally {
      this.loaded.set(true);
    }
  }

  clear(): void {
    this.context.set(undefined);
    this.loaded.set(false);
    this.forbidden.set(false);
    this.error.set(undefined);
  }

  hasPermission(permission: string): boolean {
    return this.context()?.permissions.includes(permission) ?? false;
  }
}
