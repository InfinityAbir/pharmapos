import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { KeycloakService } from '../auth/keycloak.service';

/**
 * Attaches the access token to outbound API calls. The token is refreshed through the API when
 * close to expiry, never minted locally.
 *
 * A single 401 triggers one refresh-and-retry (the token may have expired mid-session); anything
 * beyond that surfaces to the caller. Auth endpoints themselves are never retried — a rejected
 * login or refresh is a real answer, and retrying it turns server rejections into loops.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const keycloak = inject(KeycloakService);

  if (request.url.includes('/auth/login') || request.url.includes('/auth/refresh') || request.url.includes('/auth/register')) {
    return next(request);
  }

  return from(keycloak.ensureValidToken()).pipe(
    switchMap((token) => {
      const authorized = token
        ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
        : request;

      return next(authorized).pipe(
        catchError((error: unknown) => {
          if (error instanceof HttpErrorResponse && error.status === 401 && token) {
            return from(keycloak.ensureValidToken()).pipe(
              switchMap((fresh) =>
                fresh && fresh !== token
                  ? next(request.clone({ setHeaders: { Authorization: `Bearer ${fresh}` } }))
                  : throwError(() => error),
              ),
            );
          }
          return throwError(() => error);
        }),
      );
    }),
  );
};
