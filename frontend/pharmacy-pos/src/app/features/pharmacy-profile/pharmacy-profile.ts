import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { PERMISSIONS } from '../../core/auth/permissions';
import { CatalogApi, PharmacyProfile } from '../../core/http/catalog-api.service';
import { describeApiError } from '../../core/i18n/error-message';
import { TranslatePipe } from '../../core/i18n/i18n.pipes';
import { LanguageService } from '../../core/i18n/language.service';
import { SessionService } from '../../core/session/session.service';

function blankProfile(): PharmacyProfile {
  return {
    pharmacyName: '',
    address: null,
    phone: null,
    email: null,
    website: null,
    registrationInfo: null,
    vatInfo: null,
    invoiceFooter: null,
    currency: 'BDT',
    defaultLanguage: 'en',
    timezone: 'Asia/Dhaka',
    logoUrl: null,
  };
}

@Component({
  selector: 'app-pharmacy-profile',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './pharmacy-profile.html',
  styleUrl: './pharmacy-profile.css',
})
export class PharmacyProfilePage implements OnInit {
  private readonly api = inject(CatalogApi);
  protected readonly session = inject(SessionService);
  protected readonly lang = inject(LanguageService);

  protected readonly canManage = PERMISSIONS.tenantPharmacyManage;

  protected readonly loading = signal(true);
  protected readonly forbidden = signal(false);
  protected readonly notConfigured = signal(false);
  protected readonly error = signal<string | undefined>(undefined);
  protected readonly saved = signal(false);
  protected readonly saving = signal(false);

  protected profile: PharmacyProfile = blankProfile();

  ngOnInit(): void {
    void this.load();
  }

  protected hasPermission(permission: string): boolean {
    return this.session.hasPermission(permission);
  }

  protected async save(): Promise<void> {
    this.error.set(undefined);
    this.saved.set(false);

    if (!this.profile.pharmacyName.trim()) {
      this.error.set(this.lang.t('pharmacy.nameRequired'));
      return;
    }

    this.saving.set(true);
    try {
      const empty = (value: string | null): string | null =>
        value === null || value.trim() === '' ? null : value.trim();

      this.profile = await firstValueFrom(
        this.api.upsertPharmacyProfile({
          ...this.profile,
          pharmacyName: this.profile.pharmacyName.trim(),
          address: empty(this.profile.address),
          phone: empty(this.profile.phone),
          email: empty(this.profile.email),
          website: empty(this.profile.website),
          registrationInfo: empty(this.profile.registrationInfo),
          vatInfo: empty(this.profile.vatInfo),
          invoiceFooter: empty(this.profile.invoiceFooter),
          logoUrl: empty(this.profile.logoUrl),
        }),
      );
      this.notConfigured.set(false);
      this.saved.set(true);
    } catch (error) {
      this.error.set(describeApiError(this.lang, error, 'pharmacy.saveFailed'));
    } finally {
      this.saving.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(undefined);
    this.forbidden.set(false);
    this.notConfigured.set(false);

    try {
      this.profile = await firstValueFrom(this.api.getPharmacyProfile());
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.forbidden.set(true);
      } else if (error instanceof HttpErrorResponse && error.status === 404) {
        this.notConfigured.set(true);
        this.profile = blankProfile();
      } else {
        this.error.set(this.lang.t('pharmacy.loadFailed'));
      }
    } finally {
      this.loading.set(false);
    }
  }
}
