import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { KeycloakService } from '../../core/auth/keycloak.service';
import { PlatformApi, PlatformInfo } from '../../core/http/platform-api.service';
import { SessionService } from '../../core/session/session.service';

@Component({
  selector: 'app-dashboard',
  imports: [DatePipe],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard implements OnInit {
  private readonly api = inject(PlatformApi);
  private readonly keycloak = inject(KeycloakService);
  protected readonly session = inject(SessionService);

  protected readonly isAuthenticated = this.keycloak.isAuthenticated;
  protected readonly info = signal<PlatformInfo | undefined>(undefined);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | undefined>(undefined);

  ngOnInit(): void {
    void this.load();
  }

  protected signIn(): void {
    void this.keycloak.login();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(undefined);

    try {
      const info = await firstValueFrom(this.api.info());
      this.info.set(info);

      if (this.keycloak.isAuthenticated() && !this.session.loaded()) {
        await this.session.refresh();
      }
    } catch {
      this.error.set('The PharmaPOS API could not be reached.');
    } finally {
      this.loading.set(false);
    }
  }
}
