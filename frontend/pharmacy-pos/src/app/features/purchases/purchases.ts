import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { describeApiError } from '../../core/i18n/error-message';
import { LDatePipe, MoneyPipe, TranslatePipe } from '../../core/i18n/i18n.pipes';
import { LanguageService } from '../../core/i18n/language.service';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { PERMISSIONS } from '../../core/auth/permissions';
import { CatalogApi, Medicine } from '../../core/http/catalog-api.service';
import {
  Purchase,
  PurchaseInput,
  PurchaseItemInput,
  PurchasingApi,
  Supplier,
} from '../../core/http/purchasing-api.service';
import { SessionService } from '../../core/session/session.service';

interface ItemRow {
  medicineId: string;
  batchNumber: string;
  manufactureDate: string;
  expiryDate: string;
  quantity: number | null;
  unitPrice: number | null;
  discountAmount: number | null;
  sellingPrice: number | null;
}

function blankRow(): ItemRow {
  return {
    medicineId: '',
    batchNumber: '',
    manufactureDate: '',
    expiryDate: '',
    quantity: null,
    unitPrice: null,
    discountAmount: null,
    sellingPrice: null,
  };
}

function newIdempotencyKey(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID();
  }
  return `key-${Date.now()}-${Math.floor(Math.random() * 1_000_000_000)}`;
}

@Component({
  selector: 'app-purchases',
  imports: [FormsModule, TranslatePipe, MoneyPipe, LDatePipe],
  templateUrl: './purchases.html',
  styleUrl: './purchases.css',
})
export class PurchasesPage implements OnInit {
  private readonly api = inject(PurchasingApi);
  private readonly catalog = inject(CatalogApi);
  protected readonly session = inject(SessionService);
  protected readonly lang = inject(LanguageService);

  protected readonly canManage = PERMISSIONS.tenantPurchasingManage;

  protected readonly purchases = signal<readonly Purchase[]>([]);
  protected readonly suppliers = signal<readonly Supplier[]>([]);
  protected readonly medicines = signal<readonly Medicine[]>([]);
  protected readonly medicinesUnavailable = signal(false);
  protected readonly loading = signal(true);
  protected readonly forbidden = signal(false);
  protected readonly error = signal<string | undefined>(undefined);
  protected readonly notice = signal<string | undefined>(undefined);
  protected readonly saving = signal(false);

  protected view: 'list' | 'form' | 'detail' = 'list';
  protected editingId: string | undefined;
  protected detail: Purchase | undefined;

  protected filterSupplier = '';
  protected filterStatus = '';

  protected formSupplierId = '';
  protected formDate = '';
  protected formInvoice = '';
  protected formDiscount: number | null = null;
  protected formTax: number | null = null;
  protected formNotes = '';
  protected rows: ItemRow[] = [blankRow()];
  private receiveKeys = new Map<string, string>();

  ngOnInit(): void {
    void this.load();
  }

  protected hasPermission(permission: string): boolean {
    return this.session.hasPermission(permission);
  }

  protected onFilterChange(): void {
    void this.reloadPurchases();
  }

  protected startCreate(): void {
    this.editingId = undefined;
    this.view = 'form';
    this.formSupplierId = '';
    this.formDate = new Date().toISOString().slice(0, 10);
    this.formInvoice = '';
    this.formDiscount = null;
    this.formTax = null;
    this.formNotes = '';
    this.rows = [blankRow()];
    this.notice.set(undefined);
  }

  protected startEdit(purchase: Purchase): void {
    if (purchase.status !== 'Draft') {
      return;
    }
    this.editingId = purchase.id;
    this.view = 'form';
    this.formSupplierId = purchase.supplierId;
    this.formDate = purchase.purchaseDate.slice(0, 10);
    this.formInvoice = purchase.supplierInvoiceNumber ?? '';
    this.formDiscount = purchase.discountAmount;
    this.formTax = purchase.taxAmount;
    this.formNotes = purchase.notes ?? '';
    this.rows = purchase.items.map((item) => ({
      medicineId: item.medicineId,
      batchNumber: item.batchNumber,
      manufactureDate: (item.manufactureDate ?? '').slice(0, 10),
      expiryDate: item.expiryDate.slice(0, 10),
      quantity: item.quantity,
      unitPrice: item.unitPrice,
      discountAmount: item.discountAmount,
      sellingPrice: item.sellingPrice,
    }));
    this.notice.set(undefined);
  }

  protected async showDetail(purchase: Purchase): Promise<void> {
    this.notice.set(undefined);
    try {
      this.detail = await firstValueFrom(this.api.getPurchase(purchase.id));
      this.view = 'detail';
    } catch (error) {
      this.error.set(this.lang.t('purchases.loadFailed'));
    }
  }

  protected backToList(): void {
    this.view = 'list';
    this.detail = undefined;
    this.editingId = undefined;
  }

  protected addRow(): void {
    this.rows = [...this.rows, blankRow()];
  }

  protected removeRow(index: number): void {
    if (this.rows.length <= 1) {
      return;
    }
    this.rows = this.rows.filter((_, i) => i !== index);
  }

  protected rowTotal(row: ItemRow): number {
    return (row.quantity ?? 0) * (row.unitPrice ?? 0) - (row.discountAmount ?? 0);
  }

  protected formTotal(): number {
    const subtotal = this.rows.reduce((sum, row) => sum + this.rowTotal(row), 0);
    return subtotal - (this.formDiscount ?? 0) + (this.formTax ?? 0);
  }

  protected async saveDraft(): Promise<void> {
    this.notice.set(undefined);

    let input: PurchaseInput;
    try {
      input = this.buildInput();
    } catch (error) {
      this.notice.set(error instanceof Error ? error.message : 'The draft is not valid.');
      return;
    }

    this.saving.set(true);
    try {
      if (this.editingId) {
        this.detail = await firstValueFrom(this.api.updateDraft(this.editingId, input));
        this.view = 'detail';
      } else {
        this.detail = await firstValueFrom(this.api.createDraft(input));
        this.view = 'detail';
      }
      this.editingId = undefined;
      await this.reloadPurchases();
    } catch (error) {
      this.notice.set(this.describe(error, 'purchases.saveFailed'));
    } finally {
      this.saving.set(false);
    }
  }

  protected async receive(purchase: Purchase): Promise<void> {
    this.notice.set(undefined);

    let key = this.receiveKeys.get(purchase.id);
    if (!key) {
      key = newIdempotencyKey();
      this.receiveKeys.set(purchase.id, key);
    }

    this.saving.set(true);
    try {
      this.detail = await firstValueFrom(this.api.receivePurchase(purchase.id, key));
      await this.reloadPurchases();
      this.notice.set(`Purchase received. ${this.detail.items.length} line(s) posted to stock.`);
    } catch (error) {
      this.notice.set(this.describe(error, 'purchases.receiveFailed'));
    } finally {
      this.saving.set(false);
    }
  }

  protected async cancel(purchase: Purchase): Promise<void> {
    this.notice.set(undefined);
    this.saving.set(true);
    try {
      this.detail = await firstValueFrom(this.api.cancelPurchase(purchase.id));
      await this.reloadPurchases();
    } catch (error) {
      this.notice.set(this.describe(error, 'purchases.cancelFailed'));
    } finally {
      this.saving.set(false);
    }
  }

  private buildInput(): PurchaseInput {
    if (!this.formSupplierId) {
      throw new Error('A supplier is required.');
    }
    if (!this.formDate) {
      throw new Error('A purchase date is required.');
    }
    if (this.rows.length === 0) {
      throw new Error('At least one line item is required.');
    }

    const items: PurchaseItemInput[] = this.rows.map((row, index) => {
      const n = index + 1;
      if (!row.medicineId) {
        throw new Error(`Line ${n}: a medicine is required.`);
      }
      if (!row.batchNumber.trim()) {
        throw new Error(`Line ${n}: a batch number is required.`);
      }
      if (!row.expiryDate) {
        throw new Error(`Line ${n}: an expiry date is required.`);
      }
      if (!row.quantity || row.quantity < 1 || !Number.isInteger(row.quantity)) {
        throw new Error(`Line ${n}: quantity must be a positive whole number.`);
      }
      if (row.unitPrice === null || row.unitPrice < 0) {
        throw new Error(`Line ${n}: unit price cannot be negative.`);
      }
      const discount = row.discountAmount ?? 0;
      if (discount < 0 || discount > row.quantity * row.unitPrice) {
        throw new Error(`Line ${n}: discount exceeds the line gross.`);
      }

      return {
        medicineId: row.medicineId,
        batchNumber: row.batchNumber.trim(),
        manufactureDate: row.manufactureDate || null,
        expiryDate: row.expiryDate,
        quantity: row.quantity,
        unitPrice: row.unitPrice,
        discountAmount: discount,
        sellingPrice: row.sellingPrice,
      };
    });

    const empty = (value: string): string | null =>
      value.trim() === '' ? null : value.trim();

    return {
      supplierId: this.formSupplierId,
      purchaseDate: this.formDate,
      supplierInvoiceNumber: empty(this.formInvoice),
      discountAmount: this.formDiscount ?? 0,
      taxAmount: this.formTax ?? 0,
      notes: empty(this.formNotes),
      items,
    };
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(undefined);
    this.forbidden.set(false);

    try {
      const [purchases, suppliers] = await Promise.all([
        firstValueFrom(this.api.listPurchases()),
        firstValueFrom(this.api.listSuppliers()),
      ]);
      this.purchases.set(purchases);
      this.suppliers.set(suppliers);
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.forbidden.set(true);
      } else {
        this.error.set(this.lang.t('purchases.dataFailed'));
      }
    }

    if (!this.forbidden()) {
      try {
        this.medicines.set(
          await firstValueFrom(this.catalog.searchMedicines({ take: 100 })),
        );
      } catch {
        this.medicinesUnavailable.set(true);
      }
    }

    this.loading.set(false);
  }

  private async reloadPurchases(): Promise<void> {
    this.error.set(undefined);
    try {
      this.purchases.set(
        await firstValueFrom(
          this.api.listPurchases(
            this.filterSupplier || undefined,
            this.filterStatus || undefined,
          ),
        ),
      );
      if (this.detail) {
        this.detail = await firstValueFrom(this.api.getPurchase(this.detail.id));
      }
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.forbidden.set(true);
      } else {
        this.error.set(this.lang.t('purchases.reloadFailed'));
      }
    }
  }

  private describe(error: unknown, fallbackKey: string): string {
    return describeApiError(this.lang, error, fallbackKey);
  }
}
