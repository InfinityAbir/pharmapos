import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Tenant, TenancyApi } from '../../core/http/tenancy-api.service';
import { describeApiError } from '../../core/i18n/error-message';
import { TranslatePipe } from '../../core/i18n/i18n.pipes';
import { LanguageService } from '../../core/i18n/language.service';

@Component({
  selector: 'app-tenants',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './tenants.html',
  styleUrl: './tenants.css',
})
export class TenantsPage implements OnInit {
  private readonly api = inject(TenancyApi);
  protected readonly lang = inject(LanguageService);

  protected readonly tenants = signal<readonly Tenant[]>([]);
  protected readonly loading = signal(true);
  protected readonly forbidden = signal(false);
  protected readonly error = signal<string | undefined>(undefined);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | undefined>(undefined);

  protected identifier = '';
  protected name = '';

  ngOnInit(): void {
    void this.load();
  }

  protected async create(): Promise<void> {
    this.formError.set(undefined);

    const identifier = this.identifier.trim().toLowerCase();
    const name = this.name.trim();
    if (!identifier || !name) {
      this.formError.set(this.lang.t('tenants.required'));
      return;
    }

    this.saving.set(true);
    try {
      await firstValueFrom(this.api.createTenant(identifier, name));
      this.identifier = '';
      this.name = '';
      await this.load();
    } catch (error) {
      this.formError.set(this.describe(error, 'tenants.createFailed'));
    } finally {
      this.saving.set(false);
    }
  }

  protected async setStatus(tenant: Tenant, status: 'Active' | 'Suspended'): Promise<void> {
    this.error.set(undefined);
    try {
      await firstValueFrom(this.api.setTenantStatus(tenant.identifier, status));
      await this.load();
    } catch (error) {
      this.error.set(this.describe(error, 'tenants.statusFailed'));
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(undefined);
    this.forbidden.set(false);

    try {
      this.tenants.set(await firstValueFrom(this.api.listTenants()));
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.forbidden.set(true);
      } else {
        this.error.set(this.lang.t('tenants.loadFailed'));
      }
    } finally {
      this.loading.set(false);
    }
  }

  private describe(error: unknown, fallbackKey: string): string {
    if (error instanceof HttpErrorResponse) {
      const body = (error.error ?? {}) as { code?: string };
      if (!body.code && error.status === 409) {
        return this.lang.t('tenants.identifierConflict');
      }
    }
    return describeApiError(this.lang, error, fallbackKey);
  }
}
