import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { KeycloakService } from './keycloak.service';

/**
 * Gate for authenticated areas. The server is the real authority — this guard only avoids
 * rendering protected screens to an anonymous visitor by sending them to the login page.
 */
export const authGuard: CanActivateFn = () => {
  const keycloak = inject(KeycloakService);
  const router = inject(Router);

  if (keycloak.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/login']);
};
