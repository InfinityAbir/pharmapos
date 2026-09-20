import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Tenant, TenancyApi } from '../../core/http/tenancy-api.service';

@Component({
  selector: 'app-tenants',
  imports: [FormsModule],
  templateUrl: './tenants.html',
  styleUrl: './tenants.css',
})
export class TenantsPage implements OnInit {
  private readonly api = inject(TenancyApi);

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
      this.formError.set('Identifier and name are both required.');
      return;
    }

    this.saving.set(true);
    try {
      await firstValueFrom(this.api.createTenant(identifier, name));
      this.identifier = '';
      this.name = '';
      await this.load();
    } catch (error) {
      this.formError.set(this.describe(error, 'The tenant could not be created.'));
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
      this.error.set(this.describe(error, 'The tenant status could not be changed.'));
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
        this.error.set('The tenant directory could not be loaded.');
      }
    } finally {
      this.loading.set(false);
    }
  }

  private describe(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      const detail = (error.error as { detail?: string } | undefined)?.detail;
      if (typeof detail === 'string' && detail.length > 0) {
        return detail;
      }
      if (error.status === 409) {
        return 'A tenant with this identifier already exists.';
      }
    }
    return fallback;
  }
}
