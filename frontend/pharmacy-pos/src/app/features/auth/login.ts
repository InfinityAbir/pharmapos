import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { KeycloakService } from '../../core/auth/keycloak.service';
import { TranslatePipe } from '../../core/i18n/i18n.pipes';
import { LanguageService } from '../../core/i18n/language.service';
import { describeApiError } from '../../core/i18n/error-message';
import { SessionService } from '../../core/session/session.service';

@Component({
  selector: 'app-login',
  imports: [FormsModule, RouterLink, TranslatePipe],
  templateUrl: './login.html',
  styleUrl: './login.css',
})
export class LoginPage {
  private readonly keycloak = inject(KeycloakService);
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly lang = inject(LanguageService);

  protected email = '';
  protected password = '';
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | undefined>(undefined);

  constructor() {
    if (this.keycloak.isAuthenticated()) {
      void this.router.navigate(['/dashboard']);
    }
  }

  protected async submit(): Promise<void> {
    if (this.submitting()) {
      return;
    }
    this.submitting.set(true);
    this.error.set(undefined);

    try {
      await this.keycloak.login(this.email.trim(), this.password);
      await this.session.refresh();
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/dashboard';
      await this.router.navigateByUrl(returnUrl);
    } catch (failure) {
      this.error.set(describeApiError(this.lang, failure, 'errors.generic'));
    } finally {
      this.submitting.set(false);
    }
  }
}
