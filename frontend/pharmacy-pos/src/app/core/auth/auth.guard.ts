import { inject } from '@angular/core';
import { CanActivateFn } from '@angular/router';
import { KeycloakService } from './keycloak.service';

/**
 * Gate for authenticated areas. The server is the real authority — this guard only avoids
 * rendering protected screens to an anonymous visitor and starts the Keycloak login flow.
 */
export const authGuard: CanActivateFn = async () => {
  const keycloak = inject(KeycloakService);

  if (keycloak.isAuthenticated()) {
    return true;
  }

  await keycloak.login();
  return false;
};
