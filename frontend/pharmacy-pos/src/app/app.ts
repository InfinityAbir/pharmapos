import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { KeycloakService } from './core/auth/keycloak.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  protected readonly keycloak = inject(KeycloakService);
  protected readonly year = new Date().getFullYear();

  protected signIn(): void {
    void this.keycloak.login();
  }

  protected signOut(): void {
    void this.keycloak.logout();
  }
}
