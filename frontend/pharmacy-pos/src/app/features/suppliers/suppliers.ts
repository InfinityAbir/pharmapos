import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { describeApiError } from '../../core/i18n/error-message';
import { TranslatePipe } from '../../core/i18n/i18n.pipes';
import { LanguageService } from '../../core/i18n/language.service';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { PurchasingApi, Supplier, SupplierInput } from '../../core/http/purchasing-api.service';

@Component({
  selector: 'app-suppliers',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './suppliers.html',
  styleUrl: './suppliers.css',
})
export class SuppliersPage implements OnInit, OnDestroy {
  private readonly api = inject(PurchasingApi);
  protected readonly lang = inject(LanguageService);

  protected readonly suppliers = signal<readonly Supplier[]>([]);
  protected readonly loading = signal(true);
  protected readonly forbidden = signal(false);
  protected readonly error = signal<string | undefined>(undefined);
  protected readonly notice = signal<string | undefined>(undefined);
  protected readonly saving = signal(false);
  protected readonly editingId = signal<string | undefined>(undefined);

  protected search = '';
  protected form: SupplierInput = SuppliersPage.blankForm();
  private debounce: ReturnType<typeof setTimeout> | undefined;

  ngOnInit(): void {
    void this.load();
  }

  ngOnDestroy(): void {
    if (this.debounce) {
      clearTimeout(this.debounce);
    }
  }

  protected onSearchInput(): void {
    if (this.debounce) {
      clearTimeout(this.debounce);
    }
    this.debounce = setTimeout(() => void this.reload(), 300);
  }

  protected startCreate(): void {
    this.editingId.set(undefined);
    this.form = SuppliersPage.blankForm();
    this.notice.set(undefined);
  }

  protected startEdit(supplier: Supplier): void {
    this.editingId.set(supplier.id);
    this.form = {
      name: supplier.name,
      company: supplier.company ?? '',
      phone: supplier.phone ?? '',
      email: supplier.email ?? '',
      address: supplier.address ?? '',
      contactPerson: supplier.contactPerson ?? '',
      paymentTerms: supplier.paymentTerms ?? '',
    };
    this.notice.set(undefined);
  }

  protected async save(): Promise<void> {
    this.notice.set(undefined);

    const input = SuppliersPage.normalise(this.form);
    if (!input.name) {
      this.notice.set(this.lang.t('suppliers.nameRequired'));
      return;
    }

    this.saving.set(true);
    try {
      const editingId = this.editingId();
      if (editingId) {
        await firstValueFrom(this.api.updateSupplier(editingId, input));
      } else {
        await firstValueFrom(this.api.createSupplier(input));
      }
      this.startCreate();
      await this.reload();
    } catch (error) {
      this.notice.set(this.describe(error, 'suppliers.saveFailed'));
    } finally {
      this.saving.set(false);
    }
  }

  protected async setStatus(supplier: Supplier, isActive: boolean): Promise<void> {
    this.notice.set(undefined);
    try {
      await firstValueFrom(this.api.setSupplierStatus(supplier.id, isActive));
      await this.reload();
    } catch (error) {
      this.notice.set(this.describe(error, 'suppliers.statusFailed'));
    }
  }

  protected async remove(supplier: Supplier): Promise<void> {
    this.notice.set(undefined);
    try {
      await firstValueFrom(this.api.deleteSupplier(supplier.id));
      await this.reload();
    } catch (error) {
      this.notice.set(this.describe(error, 'suppliers.deleteFailed'));
    }
  }

  private static blankForm(): SupplierInput {
    return {
      name: '',
      company: '',
      phone: '',
      email: '',
      address: '',
      contactPerson: '',
      paymentTerms: '',
    };
  }

  private static normalise(form: SupplierInput): SupplierInput {
    const empty = (value: string | null | undefined): string | null =>
      value === null || value === undefined || value.trim() === '' ? null : value.trim();

    return {
      name: form.name.trim(),
      company: empty(form.company),
      phone: empty(form.phone),
      email: empty(form.email),
      address: empty(form.address),
      contactPerson: empty(form.contactPerson),
      paymentTerms: empty(form.paymentTerms),
    };
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(undefined);
    this.forbidden.set(false);

    try {
      this.suppliers.set(await firstValueFrom(this.api.listSuppliers()));
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.forbidden.set(true);
      } else {
        this.error.set(this.lang.t('suppliers.loadFailed'));
      }
    } finally {
      this.loading.set(false);
    }
  }

  private async reload(): Promise<void> {
    this.error.set(undefined);
    try {
      this.suppliers.set(
        await firstValueFrom(this.api.listSuppliers(this.search.trim() || undefined)),
      );
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.forbidden.set(true);
      } else {
        this.error.set(this.lang.t('suppliers.reloadFailed'));
      }
    }
  }

  private describe(error: unknown, fallbackKey: string): string {
    return describeApiError(this.lang, error, fallbackKey);
  }
}
