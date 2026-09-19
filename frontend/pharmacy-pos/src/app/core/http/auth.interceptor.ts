import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { from, switchMap } from 'rxjs';
import { KeycloakService } from '../auth/keycloak.service';

/**
 * Attaches the Keycloak access token to outbound API calls. The token is refreshed (via Keycloak)
 * when it is close to expiry, never minted locally.
 *
 * A 401 is intentionally surfaced to the caller rather than triggering a redirect here: the route
 * guard owns the login flow, and redirecting from the interceptor turns any server-side rejection
 * into an infinite login loop.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const keycloak = inject(KeycloakService);

  return from(keycloak.ensureValidToken()).pipe(
    switchMap((token) => {
      const authorized = token
        ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
        : request;

      return next(authorized);
    }),
  );
};
