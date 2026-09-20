import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { PERMISSIONS } from '../../core/auth/permissions';
import { CatalogApi, Medicine } from '../../core/http/catalog-api.service';
import { Batch, InventoryApi } from '../../core/http/inventory-api.service';
import {
  CheckoutPaymentInput,
  PAYMENT_METHODS,
  Receipt,
  SalesApi,
} from '../../core/http/sales-api.service';
import { SessionService } from '../../core/session/session.service';
import { ReceiptView } from '../sales/receipt';

interface CartLine {
  medicineId: string;
  name: string;
  strength: string;
  estimatePrice: number;
  quantity: number;
  discount: number;
  batchId: string;
  batches: Batch[];
}

interface PayRow {
  method: string;
  amount: number | null;
  reference: string;
}

function blankPayRow(): PayRow {
  return { method: 'Cash', amount: null, reference: '' };
}

function newIdempotencyKey(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID();
  }
  return `key-${Date.now()}-${Math.floor(Math.random() * 1_000_000_000)}`;
}

@Component({
  selector: 'app-pos',
  imports: [FormsModule, ReceiptView],
  templateUrl: './pos.html',
  styleUrl: './pos.css',
})
export class PosPage implements OnInit, OnDestroy {
  private readonly sales = inject(SalesApi);
  private readonly catalog = inject(CatalogApi);
  private readonly inventory = inject(InventoryApi);
  protected readonly session = inject(SessionService);

  protected readonly canManage = PERMISSIONS.tenantSalesManage;
  protected readonly paymentMethods = PAYMENT_METHODS;

  protected readonly results = signal<readonly Medicine[]>([]);
  protected readonly searching = signal(false);
  protected readonly forbidden = signal(false);
  protected readonly error = signal<string | undefined>(undefined);
  protected readonly completing = signal(false);

  protected search = '';
  protected cart: CartLine[] = [];
  protected payments: PayRow[] = [blankPayRow()];
  protected headerDiscount: number | null = null;
  protected customerName = '';
  protected customerPhone = '';
  protected prescriptionReference = '';
  protected receipt: Receipt | undefined;
  protected cartKey = newIdempotencyKey();
  private debounce: ReturnType<typeof setTimeout> | undefined;

  ngOnInit(): void {
    void this.initialSearch();
  }

  ngOnDestroy(): void {
    if (this.debounce) {
      clearTimeout(this.debounce);
    }
  }

  protected hasPermission(permission: string): boolean {
    return this.session.hasPermission(permission);
  }

  protected onSearchInput(): void {
    if (this.debounce) {
      clearTimeout(this.debounce);
    }
    this.debounce = setTimeout(() => void this.runSearch(), 300);
  }

  /** Scanner fast-path: Enter first tries an exact barcode match (adds immediately). */
  protected async onSearchEnter(): Promise<void> {
    const term = this.search.trim();
    if (!term) {
      return;
    }

    try {
      const exact = await firstValueFrom(this.catalog.getMedicineByBarcode(term));
      await this.addToCart(exact);
      this.search = '';
      await this.runSearch();
    } catch {
      await this.runSearch();
    }
  }

  protected async addToCart(medicine: Medicine): Promise<void> {
    const existing = this.cart.find((line) => line.medicineId === medicine.id);
    if (existing) {
      existing.quantity += 1;
      this.cart = [...this.cart];
      return;
    }

    let batches: Batch[] = [];
    try {
      batches = [...(await firstValueFrom(this.inventory.listBatches(medicine.id)))];
    } catch {
      batches = [];
    }

    this.cart = [
      ...this.cart,
      {
        medicineId: medicine.id,
        name: medicine.brandName,
        strength: medicine.strength,
        estimatePrice: medicine.sellingPrice,
        quantity: 1,
        discount: 0,
        batchId: '',
        batches,
      },
    ];
  }

  protected changeQty(line: CartLine, delta: number): void {
    line.quantity = Math.max(1, line.quantity + delta);
    this.cart = [...this.cart];
  }

  protected removeLine(line: CartLine): void {
    this.cart = this.cart.filter((entry) => entry !== line);
  }

  protected addPayRow(): void {
    if (this.payments.length < 4) {
      this.payments = [...this.payments, blankPayRow()];
    }
  }

  protected removePayRow(index: number): void {
    if (this.payments.length > 1) {
      this.payments = this.payments.filter((_, i) => i !== index);
    }
  }

  protected estimateSubtotal(): number {
    return this.cart.reduce((sum, line) => sum + line.quantity * line.estimatePrice, 0);
  }

  protected estimateLineDiscount(): number {
    return this.cart.reduce((sum, line) => sum + (line.discount || 0), 0);
  }

  protected estimateTotal(): number {
    return Math.max(
      0,
      this.estimateSubtotal() - this.estimateLineDiscount() - (this.headerDiscount || 0),
    );
  }

  protected paidTotal(): number {
    return this.payments.reduce((sum, row) => sum + (row.amount || 0), 0);
  }

  protected changeDue(): number {
    return this.paidTotal() - this.estimateTotal();
  }

  protected async complete(): Promise<void> {
    this.error.set(undefined);

    if (this.cart.length === 0) {
      this.error.set('The cart is empty.');
      return;
    }

    const payments: CheckoutPaymentInput[] = [];
    for (const [index, row] of this.payments.entries()) {
      if (row.amount === null || row.amount <= 0) {
        this.error.set(`Payment ${index + 1}: enter an amount greater than zero.`);
        return;
      }
      if (row.method !== 'Cash' && !row.reference.trim()) {
        this.error.set(`Payment ${index + 1}: a transaction reference is required.`);
        return;
      }
      payments.push({
        method: row.method,
        amount: row.amount,
        reference: row.reference.trim() || null,
      });
    }

    this.completing.set(true);
    try {
      const sale = await firstValueFrom(
        this.sales.checkout({
          items: this.cart.map((line) => ({
            medicineId: line.medicineId,
            quantity: line.quantity,
            batchId: line.batchId || null,
            discountAmount: line.discount || 0,
          })),
          payments,
          discountAmount: this.headerDiscount || 0,
          customerName: this.customerName.trim() || null,
          customerPhone: this.customerPhone.trim() || null,
          prescriptionReference: this.prescriptionReference.trim() || null,
          idempotencyKey: this.cartKey,
        }),
      );

      this.receipt = await firstValueFrom(this.sales.getReceipt(sale.id));
    } catch (error) {
      this.error.set(this.describe(error, 'The sale could not be completed.'));
    } finally {
      this.completing.set(false);
    }
  }

  protected newSale(): void {
    this.cart = [];
    this.payments = [blankPayRow()];
    this.headerDiscount = null;
    this.customerName = '';
    this.customerPhone = '';
    this.prescriptionReference = '';
    this.receipt = undefined;
    this.cartKey = newIdempotencyKey();
    this.error.set(undefined);
    this.search = '';
    void this.runSearch();
  }

  private async initialSearch(): Promise<void> {
    await this.runSearch();
  }

  private async runSearch(): Promise<void> {
    this.searching.set(true);
    this.error.set(undefined);
    try {
      this.results.set(
        await firstValueFrom(
          this.catalog.searchMedicines({
            search: this.search.trim() || undefined,
            isActive: true,
            take: 20,
          }),
        ),
      );
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.forbidden.set(true);
      } else {
        this.error.set('Search could not be completed.');
      }
    } finally {
      this.searching.set(false);
    }
  }

  private describe(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      const detail = (error.error as { detail?: string } | undefined)?.detail;
      if (typeof detail === 'string' && detail.length > 0) {
        return detail;
      }
      if (error.status === 422) {
        return 'The sale was refused by a checkout rule (see details above).';
      }
      if (error.status === 409) {
        return 'Stock changed while checking out. Review the cart and retry.';
      }
    }
    return fallback;
  }
}
