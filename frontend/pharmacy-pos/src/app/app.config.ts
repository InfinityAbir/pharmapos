import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { KeycloakService } from './core/auth/keycloak.service';
import { AppConfigService } from './core/config/app-config.service';
import { authInterceptor } from './core/http/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),

    // Initializers run concurrently, so config load and Keycloak setup are chained explicitly:
    // configuration must be resolved before the OIDC client is created.
    provideAppInitializer(() => {
      const appConfig = inject(AppConfigService);
      const keycloak = inject(KeycloakService);

      return appConfig.load().then(() => keycloak.init());
    }),
  ],
};
