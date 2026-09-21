import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { KeycloakService } from './keycloak.service';
import { SessionService } from '../session/session.service';

/**
 * Route gate for a realm-role-derived permission. Anonymous visitors go to the login page;
 * authenticated callers without the permission fall back to the dashboard. The API remains
 * the real authority — this only avoids rendering screens the caller cannot use.
 */
export function permissionGuard(requiredPermission: string): CanActivateFn {
  return async (): Promise<boolean> => {
    const keycloak = inject(KeycloakService);
    const session = inject(SessionService);
    const router = inject(Router);

    if (!keycloak.isAuthenticated()) {
      await router.navigate(['/login']);
      return false;
    }

    if (!session.loaded()) {
      await session.refresh();
    }

    if (session.hasPermission(requiredPermission)) {
      return true;
    }

    await router.navigate(['/dashboard']);
    return false;
  };
}
