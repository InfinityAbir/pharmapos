import { Component, OnDestroy, OnInit, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { PERMISSIONS } from '../../core/auth/permissions';
import { CatalogApi, Medicine } from '../../core/http/catalog-api.service';
import { Batch, InventoryApi } from '../../core/http/inventory-api.service';
import {
  CheckoutInput,
  CheckoutPaymentInput,
  PAYMENT_METHODS,
  Receipt,
  Sale,
  SalesApi,
} from '../../core/http/sales-api.service';
import { Customer, CustomersApi } from '../../core/http/customers-api.service';
import { CatalogCacheService } from '../../core/offline/catalog-cache.service';
import { ConnectivityService } from '../../core/offline/connectivity.service';
import { OfflineQueueService, QueuedCheckout } from '../../core/offline/offline-queue.service';
import { describeApiError } from '../../core/i18n/error-message';
import { LDateTimePipe, MoneyPipe, TranslatePipe } from '../../core/i18n/i18n.pipes';
import { LanguageService } from '../../core/i18n/language.service';
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
  imports: [FormsModule, ReceiptView, TranslatePipe, MoneyPipe, LDateTimePipe],
  templateUrl: './pos.html',
  styleUrl: './pos.css',
})
export class PosPage implements OnInit, OnDestroy {
  private readonly sales = inject(SalesApi);
  private readonly catalog = inject(CatalogApi);
  private readonly inventory = inject(InventoryApi);
  private readonly customers = inject(CustomersApi);
  protected readonly session = inject(SessionService);
  protected readonly lang = inject(LanguageService);
  protected readonly connectivity = inject(ConnectivityService);
  protected readonly queue = inject(OfflineQueueService);
  private readonly catalogCache = inject(CatalogCacheService);

  protected readonly canManage = PERMISSIONS.tenantSalesManage;
  protected readonly paymentMethods = PAYMENT_METHODS;

  protected readonly results = signal<readonly Medicine[]>([]);
  protected readonly searching = signal(false);
  protected readonly forbidden = signal(false);
  protected readonly error = signal<string | undefined>(undefined);
  protected readonly completing = signal(false);
  protected readonly customerResults = signal<readonly Customer[]>([]);
  protected readonly customerSearching = signal(false);
  protected readonly selectedCustomer = signal<Customer | undefined>(undefined);

  protected search = '';
  protected cart: CartLine[] = [];
  protected payments: PayRow[] = [blankPayRow()];
  protected headerDiscount: number | null = null;
  protected customerSearch = '';
  protected customerName = '';
  protected customerPhone = '';
  protected prescriptionReference = '';
  protected receipt: Receipt | undefined;
  protected cartKey = newIdempotencyKey();
  protected readonly queuedNotice = signal<string | undefined>(undefined);
  protected readonly showingCached = signal(false);
  protected readonly foreignPending = signal(false);
  private debounce: ReturnType<typeof setTimeout> | undefined;
  private customerDebounce: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    // Reconnect (or reload with pending work) replays this tenant's queue. The sweep is a
    // no-op when nothing is pending and never touches the active cart.
    effect(() => {
      if (this.connectivity.online()) {
        void this.autoSync();
      }
    });
  }

  ngOnInit(): void {
    void this.initialSearch();
  }

  ngOnDestroy(): void {
    if (this.debounce) {
      clearTimeout(this.debounce);
    }
    if (this.customerDebounce) {
      clearTimeout(this.customerDebounce);
    }
  }

  protected hasPermission(permission: string): boolean {
    return this.session.hasPermission(permission);
  }

  protected canUseCustomers(): boolean {
    return (
      this.session.hasPermission('tenant.customers.read') ||
      this.session.hasPermission('tenant.customers.manage')
    );
  }

  protected onCustomerSearchInput(): void {
    if (this.customerDebounce) {
      clearTimeout(this.customerDebounce);
    }
    this.customerDebounce = setTimeout(() => void this.runCustomerSearch(), 300);
  }

  protected async runCustomerSearch(): Promise<void> {
    const term = this.customerSearch.trim();
    if (!term) {
      this.customerResults.set([]);
      return;
    }
    this.customerSearching.set(true);
    try {
      this.customerResults.set(await firstValueFrom(this.customers.listCustomers(term)));
    } catch {
      this.customerResults.set([]);
    } finally {
      this.customerSearching.set(false);
    }
  }

  protected selectCustomer(customer: Customer): void {
    this.selectedCustomer.set(customer);
    this.customerName = customer.name;
    this.customerPhone = customer.phone ?? '';
    this.customerResults.set([]);
    this.customerSearch = '';
  }

  protected clearCustomer(): void {
    this.selectedCustomer.set(undefined);
    this.customerName = '';
    this.customerPhone = '';
  }

  protected async quickCreateCustomer(): Promise<void> {
    this.error.set(undefined);
    const name = this.customerName.trim() || this.customerSearch.trim();
    if (!name) {
      this.error.set(this.lang.t('pos.customerNameRequired'));
      return;
    }
    try {
      const created = await firstValueFrom(
        this.customers.createCustomer({
          name,
          phone: this.customerPhone.trim() || null,
          email: null,
          address: null,
          notes: null,
        }),
      );
      this.selectCustomer(created);
    } catch (error) {
      this.error.set(this.describe(error, 'pos.customerCreateFailed'));
    }
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
    this.queuedNotice.set(undefined);

    if (this.cart.length === 0) {
      this.error.set(this.lang.t('pos.cartEmptyError'));
      return;
    }

    const payments = this.readPayments();
    if (!payments) {
      return;
    }

    const input: CheckoutInput = {
      items: this.cart.map((line) => ({
        medicineId: line.medicineId,
        quantity: line.quantity,
        batchId: line.batchId || null,
        discountAmount: line.discount || 0,
      })),
      payments,
      discountAmount: this.headerDiscount || 0,
      customerId: this.selectedCustomer()?.id ?? null,
      customerName: this.customerName.trim() || null,
      customerPhone: this.customerPhone.trim() || null,
      prescriptionReference: this.prescriptionReference.trim() || null,
      idempotencyKey: this.cartKey,
    };

    if (await this.connectivity.ensureOnline()) {
      await this.checkoutOnline(input);
      return;
    }

    // Offline: capture the intent (with its idempotency key) and clear the cart for the
    // next customer. Sync replays it; the server dedupes retries and revalidates stock.
    const tenantId = this.session.context()?.tenantIdentifier;
    if (!tenantId) {
      this.error.set(this.lang.t('pos.checkoutFailed'));
      return;
    }
    this.queue.enqueue(tenantId, input);
    this.queuedNotice.set(this.lang.t('offline.queuedDone'));
    this.newSale();
  }

  /** Queued intents for the current tenant session (newest last). */
  protected queuedForTenant(): readonly QueuedCheckout[] {
    const tenantId = this.session.context()?.tenantIdentifier;
    if (!tenantId) {
      return [];
    }
    return this.queue.items().filter((entry) => entry.tenantId === tenantId);
  }

  protected async syncNow(): Promise<void> {
    this.queuedNotice.set(undefined);
    await this.autoSync(true);
  }

  protected retryEntry(entry: QueuedCheckout): void {
    this.queue.markRetrying(entry.id);
    void this.autoSync(true);
  }

  protected dismissEntry(entry: QueuedCheckout): void {
    this.queue.remove(entry.id);
  }

  protected clearConfirmed(): void {
    this.queue.clearConfirmed();
  }

  protected async viewQueuedReceipt(entry: QueuedCheckout): Promise<void> {
    if (!entry.saleId) {
      return;
    }
    this.error.set(undefined);
    if (!(await this.connectivity.ensureOnline())) {
      this.error.set(this.lang.t('offline.requiresConnection'));
      return;
    }
    try {
      this.receipt = await firstValueFrom(this.sales.getReceipt(entry.saleId));
    } catch (error) {
      this.error.set(describeApiError(this.lang, error, 'sales.receiptFailed'));
    }
  }

  protected describeQueued(entry: QueuedCheckout): string {
    if (!entry.failure) {
      return this.lang.t(entry.status === 'confirmed' ? 'offline.synced' : 'offline.pending');
    }
    if (entry.failure.code) {
      const key = `errors.${entry.failure.code.replaceAll('.', '_')}`;
      const translated = this.lang.t(key);
      if (translated !== key) {
        return translated;
      }
    }
    return entry.failure.detail || this.lang.t('errors.generic');
  }

  private async autoSync(announce = false): Promise<void> {
    const tenantId = this.session.context()?.tenantIdentifier;
    if (!tenantId) {
      return;
    }
    const foreign = this.queue.items().some(
      (entry) =>
        entry.tenantId !== tenantId &&
        (entry.status === 'queued' || entry.status === 'failed'),
    );
    this.foreignPending.set(foreign);
    const before = this.queue.pendingForTenant(tenantId).length;
    await this.queue.syncAll(tenantId, (input) => this.sales.checkout(input));
    if (announce) {
      const after = this.queue.pendingForTenant(tenantId).length;
      if (before > 0 && after === 0) {
        this.queuedNotice.set(this.lang.t('offline.synced'));
      }
    }
  }

  private readPayments(): CheckoutPaymentInput[] | undefined {
    const payments: CheckoutPaymentInput[] = [];
    for (const [index, row] of this.payments.entries()) {
      if (row.amount === null || row.amount <= 0) {
        this.error.set(`Payment ${index + 1}: ${this.lang.t('pos.paymentAmountError')}`);
        return undefined;
      }
      if (row.method !== 'Cash' && !row.reference.trim()) {
        this.error.set(`Payment ${index + 1}: ${this.lang.t('pos.paymentReferenceError')}`);
        return undefined;
      }
      payments.push({
        method: row.method,
        amount: row.amount,
        reference: row.reference.trim() || null,
      });
    }
    return payments;
  }

  private async checkoutOnline(input: CheckoutInput): Promise<void> {
    this.completing.set(true);
    try {
      const sale = await firstValueFrom(this.sales.checkout(input));
      this.receipt = await firstValueFrom(this.sales.getReceipt(sale.id));
    } catch (error) {
      this.error.set(this.describe(error, 'pos.checkoutFailed'));
    } finally {
      this.completing.set(false);
    }
  }

  protected newSale(): void {
    this.cart = [];
    this.payments = [blankPayRow()];
    this.headerDiscount = null;
    this.selectedCustomer.set(undefined);
    this.customerSearch = '';
    this.customerResults.set([]);
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
    this.showingCached.set(false);
    try {
      const results = await firstValueFrom(
        this.catalog.searchMedicines({
          search: this.search.trim() || undefined,
          isActive: true,
          take: 20,
        }),
      );
      this.results.set(results);
      this.catalogCache.store(results);
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.forbidden.set(true);
      } else if (typeof navigator !== 'undefined' && !navigator.onLine) {
        // Offline: display-only fallback from recently seen medicines. Prices and stock
        // are re-resolved server-side; the sale itself queues until reconnect.
        const term = this.search.trim();
        this.results.set(
          this.catalogCache.search(term).map((cached) => ({
            id: cached.id,
            brandName: cached.brandName,
            genericName: cached.genericName,
            strength: cached.strength,
            dosageForm: '',
            manufacturerId: null,
            manufacturerName: null,
            categoryId: null,
            categoryName: null,
            barcode: null,
            unit: '',
            packSize: null,
            purchasePrice: 0,
            sellingPrice: cached.sellingPrice,
            reorderLevel: 0,
            vatRate: null,
            requiresPrescription: false,
            isActive: true,
          })),
        );
        this.showingCached.set(true);
      } else {
        this.error.set(this.lang.t('pos.searchFailed'));
      }
    } finally {
      this.searching.set(false);
    }
  }

  private describe(error: unknown, fallbackKey: string): string {
    if (error instanceof HttpErrorResponse) {
      const body = (error.error ?? {}) as { code?: string };
      if (!body.code) {
        if (error.status === 422) {
          return this.lang.t('pos.saleRefused');
        }
        if (error.status === 409) {
          return this.lang.t('pos.stockChanged');
        }
      }
    }
    return describeApiError(this.lang, error, fallbackKey);
  }
}
