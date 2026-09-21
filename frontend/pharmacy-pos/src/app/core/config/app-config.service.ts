import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface OidcConfig {
  readonly url: string;
  readonly realm: string;
  readonly clientId: string;
}

export interface AppConfig {
  readonly apiBaseUrl: string;
  /**
   * Legacy OIDC block, ignored since the SPA authenticates through the API's
   * app-owned login. Optional so older config.json files keep loading.
   */
  readonly oidc?: OidcConfig;
}

/**
 * Loads deployment-specific configuration from `/config.json` at startup so one build can be
 * promoted across environments without recompiling. Nothing here is a secret: the SPA is a public
 * OIDC client, and the API never trusts client-supplied tenant or permission values.
 */
@Injectable({ providedIn: 'root' })
export class AppConfigService {
  private readonly http = inject(HttpClient);
  private readonly _config = signal<AppConfig | undefined>(undefined);

  get value(): AppConfig {
    const config = this._config();
    if (!config) {
      throw new Error('Application configuration has not been loaded.');
    }
    return config;
  }

  get apiBaseUrl(): string {
    return this.value.apiBaseUrl;
  }

  load(): Promise<void> {
    return firstValueFrom(this.http.get<AppConfig>('/config.json')).then((config) =>
      this._config.set(config),
    );
  }
}
