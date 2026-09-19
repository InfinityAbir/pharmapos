import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { KeycloakService } from '../../core/auth/keycloak.service';
import { PlatformApi, PlatformInfo, WhoAmI } from '../../core/http/platform-api.service';

@Component({
  selector: 'app-dashboard',
  imports: [DatePipe],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard implements OnInit {
  private readonly api = inject(PlatformApi);
  private readonly keycloak = inject(KeycloakService);

  protected readonly isAuthenticated = this.keycloak.isAuthenticated;
  protected readonly info = signal<PlatformInfo | undefined>(undefined);
  protected readonly who = signal<WhoAmI | undefined>(undefined);
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

      if (!this.keycloak.isAuthenticated()) {
        this.info.set(info);
        return;
      }

      const who = await firstValueFrom(this.api.whoami());
      this.info.set(info);
      this.who.set(who);
    } catch {
      this.error.set('The PharmaPOS API could not be reached.');
    } finally {
      this.loading.set(false);
    }
  }
}
