import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { PERMISSIONS } from '../../core/auth/permissions';
import {
  CatalogApi,
  Category,
  DOSAGE_FORMS,
  Manufacturer,
  Medicine,
  MedicineInput,
} from '../../core/http/catalog-api.service';
import { SessionService } from '../../core/session/session.service';

@Component({
  selector: 'app-medicines',
  imports: [FormsModule],
  templateUrl: './medicines.html',
  styleUrl: './medicines.css',
})
export class MedicinesPage implements OnInit, OnDestroy {
  private readonly api = inject(CatalogApi);
  protected readonly session = inject(SessionService);

  protected readonly canManage = PERMISSIONS.tenantMedicinesManage;
  protected readonly dosageForms = DOSAGE_FORMS;

  protected readonly medicines = signal<readonly Medicine[]>([]);
  protected readonly categories = signal<readonly Category[]>([]);
  protected readonly manufacturers = signal<readonly Manufacturer[]>([]);
  protected readonly loading = signal(true);
  protected readonly forbidden = signal(false);
  protected readonly error = signal<string | undefined>(undefined);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | undefined>(undefined);
  protected readonly editingId = signal<string | undefined>(undefined);

  protected search = '';
  protected filterCategory = '';
  protected filterManufacturer = '';
  protected filterActive = 'active';
  private debounce: ReturnType<typeof setTimeout> | undefined;

  protected form: MedicineInput = MedicinesPage.blankForm();

  ngOnInit(): void {
    void this.loadAll();
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

  protected onFilterChange(): void {
    void this.reload();
  }

  protected hasPermission(permission: string): boolean {
    return this.session.hasPermission(permission);
  }

  protected startCreate(): void {
    this.editingId.set(undefined);
    this.form = MedicinesPage.blankForm();
    this.formError.set(undefined);
  }

  protected startEdit(medicine: Medicine): void {
    this.editingId.set(medicine.id);
    this.form = {
      brandName: medicine.brandName,
      genericName: medicine.genericName,
      strength: medicine.strength,
      dosageForm: medicine.dosageForm,
      manufacturerId: medicine.manufacturerId,
      categoryId: medicine.categoryId,
      barcode: medicine.barcode ?? '',
      unit: medicine.unit,
      packSize: medicine.packSize,
      purchasePrice: medicine.purchasePrice,
      sellingPrice: medicine.sellingPrice,
      reorderLevel: medicine.reorderLevel,
      vatRate: medicine.vatRate,
      requiresPrescription: medicine.requiresPrescription,
    };
    this.formError.set(undefined);
  }

  protected async save(): Promise<void> {
    this.formError.set(undefined);

    const input = MedicinesPage.normalise(this.form);
    if (!input.brandName || !input.genericName || !input.strength || !input.unit) {
      this.formError.set('Brand, generic, strength and unit are all required.');
      return;
    }

    this.saving.set(true);
    try {
      const editingId = this.editingId();
      if (editingId) {
        await firstValueFrom(this.api.updateMedicine(editingId, input));
      } else {
        await firstValueFrom(this.api.createMedicine(input));
      }
      this.startCreate();
      await this.reload();
    } catch (error) {
      this.formError.set(this.describe(error, 'The medicine could not be saved.'));
    } finally {
      this.saving.set(false);
    }
  }

  protected async setStatus(medicine: Medicine, isActive: boolean): Promise<void> {
    this.error.set(undefined);
    try {
      await firstValueFrom(this.api.setMedicineStatus(medicine.id, isActive));
      await this.reload();
    } catch (error) {
      this.error.set(this.describe(error, 'The medicine status could not be changed.'));
    }
  }

  private static blankForm(): MedicineInput {
    return {
      brandName: '',
      genericName: '',
      strength: '',
      dosageForm: 'Tablet',
      manufacturerId: null,
      categoryId: null,
      barcode: '',
      unit: 'strip',
      packSize: null,
      purchasePrice: 0,
      sellingPrice: 0,
      reorderLevel: 0,
      vatRate: null,
      requiresPrescription: false,
    };
  }

  private static normalise(form: MedicineInput): MedicineInput {
    const empty = (value: string | null | undefined): string | null =>
      value === null || value === undefined || value.trim() === '' ? null : value.trim();

    return {
      ...form,
      brandName: form.brandName.trim(),
      genericName: form.genericName.trim(),
      strength: form.strength.trim(),
      unit: form.unit.trim(),
      barcode: empty(form.barcode),
      manufacturerId: form.manufacturerId || null,
      categoryId: form.categoryId || null,
    };
  }

  private async loadAll(): Promise<void> {
    this.loading.set(true);
    this.error.set(undefined);
    this.forbidden.set(false);

    try {
      const [medicines, categories, manufacturers] = await Promise.all([
        firstValueFrom(this.api.searchMedicines({})),
        firstValueFrom(this.api.listCategories()),
        firstValueFrom(this.api.listManufacturers()),
      ]);
      this.medicines.set(medicines);
      this.categories.set(categories);
      this.manufacturers.set(manufacturers);
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.forbidden.set(true);
      } else {
        this.error.set('The medicine catalog could not be loaded.');
      }
    } finally {
      this.loading.set(false);
    }
  }

  private async reload(): Promise<void> {
    this.error.set(undefined);
    try {
      this.medicines.set(
        await firstValueFrom(
          this.api.searchMedicines({
            search: this.search.trim() || undefined,
            categoryId: this.filterCategory || undefined,
            manufacturerId: this.filterManufacturer || undefined,
            isActive: this.filterActive === 'all' ? undefined : this.filterActive === 'active',
            take: 50,
          }),
        ),
      );
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.forbidden.set(true);
      } else {
        this.error.set('The search could not be completed.');
      }
    }
  }

  private describe(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      const detail = (error.error as { detail?: string } | undefined)?.detail;
      if (typeof detail === 'string' && detail.length > 0) {
        return detail;
      }
      if (error.status === 409) {
        return 'Another medicine already uses this barcode.';
      }
      if (error.status === 404) {
        return 'The selected category or manufacturer no longer exists. Reload and retry.';
      }
    }
    return fallback;
  }
}
