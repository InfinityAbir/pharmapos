import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AppConfigService } from '../config/app-config.service';

export interface TokenResponse {
  readonly accessToken: string;
  readonly refreshToken: string | null;
  readonly expiresInSeconds: number;
  readonly tokenType: string;
}

export interface RegisterPayload {
  readonly email: string;
  readonly password: string;
  readonly firstName: string;
  readonly lastName: string;
  readonly pharmacyName: string;
}

/**
 * App-owned authentication endpoints. The API proxies Keycloak server-side; tokens come back
 * here and are carried by KeycloakService. No OIDC redirects, no keycloak-js.
 */
@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);
  private readonly appConfig = inject(AppConfigService);

  private get baseUrl(): string {
    return `${this.appConfig.apiBaseUrl}/auth`;
  }

  login(email: string, password: string): Observable<TokenResponse> {
    return this.http.post<TokenResponse>(`${this.baseUrl}/login`, { email, password });
  }

  refresh(refreshToken: string): Observable<TokenResponse> {
    return this.http.post<TokenResponse>(`${this.baseUrl}/refresh`, { refreshToken });
  }

  register(payload: RegisterPayload): Observable<TokenResponse> {
    return this.http.post<TokenResponse>(`${this.baseUrl}/register`, payload);
  }
}
