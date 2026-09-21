import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { KeycloakService } from '../../core/auth/keycloak.service';
import { TranslatePipe } from '../../core/i18n/i18n.pipes';
import { LanguageService } from '../../core/i18n/language.service';
import { describeApiError } from '../../core/i18n/error-message';
import { SessionService } from '../../core/session/session.service';

@Component({
  selector: 'app-register',
  imports: [FormsModule, RouterLink, TranslatePipe],
  templateUrl: './register.html',
  styleUrl: './login.css',
})
export class RegisterPage {
  private readonly keycloak = inject(KeycloakService);
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly lang = inject(LanguageService);

  protected pharmacyName = '';
  protected firstName = '';
  protected lastName = '';
  protected email = '';
  protected password = '';
  protected confirmPassword = '';
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
    if (this.password !== this.confirmPassword) {
      this.error.set(this.lang.t('auth.passwordMismatch'));
      return;
    }

    this.submitting.set(true);
    this.error.set(undefined);

    try {
      await this.keycloak.register({
        email: this.email.trim(),
        password: this.password,
        firstName: this.firstName.trim(),
        lastName: this.lastName.trim(),
        pharmacyName: this.pharmacyName.trim(),
      });
      await this.session.refresh();
      await this.router.navigate(['/dashboard']);
    } catch (failure) {
      this.error.set(describeApiError(this.lang, failure, 'errors.generic'));
    } finally {
      this.submitting.set(false);
    }
  }
}
