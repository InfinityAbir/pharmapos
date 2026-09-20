import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { PERMISSIONS } from '../../core/auth/permissions';
import { PAYMENT_METHODS, Receipt, Sale, SalesApi } from '../../core/http/sales-api.service';
import {
  ProcessReturnInput,
  ReturnableSale,
  ReturnsApi,
} from '../../core/http/returns-api.service';
import { SessionService } from '../../core/session/session.service';
import { ReceiptView } from './receipt';

function newIdempotencyKey(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID();
  }
  return `key-${Date.now()}-${Math.floor(Math.random() * 1_000_000_000)}`;
}

@Component({
  selector: 'app-sales',
  imports: [FormsModule, DatePipe, ReceiptView],
  templateUrl: './sales.html',
  styleUrl: './sales.css',
})
export class SalesPage implements OnInit {
  private readonly api = inject(SalesApi);
  private readonly returns = inject(ReturnsApi);
  private readonly session = inject(SessionService);

  protected readonly sales = signal<readonly Sale[]>([]);
  protected readonly receipt = signal<Receipt | undefined>(undefined);
  protected readonly loading = signal(true);
  protected readonly forbidden = signal(false);
  protected readonly error = signal<string | undefined>(undefined);
  protected readonly returnable = signal<ReturnableSale | undefined>(undefined);
  protected readonly returnLoading = signal(false);
  protected readonly returnSaving = signal(false);
  protected readonly returnNotice = signal<string | undefined>(undefined);
  protected readonly paymentMethods = PAYMENT_METHODS;

  protected from = '';
  protected to = '';
  protected returnQty: Record<string, number> = {};
  protected returnReason = '';
  protected returnRestock = true;
  protected returnMethod = 'Cash';
  protected returnReference = '';

  ngOnInit(): void {
    void this.load();
  }

  protected canManageReturns(): boolean {
    return this.session.hasPermission(PERMISSIONS.tenantReturnsManage);
  }

  protected canReadReturns(): boolean {
    return this.session.hasPermission(PERMISSIONS.tenantReturnsRead);
  }

  protected onFilterChange(): void {
    void this.reload();
  }

  protected async showReceipt(sale: Sale): Promise<void> {
    this.error.set(undefined);
    try {
      this.receipt.set(await firstValueFrom(this.api.getReceipt(sale.id)));
    } catch {
      this.error.set('The receipt could not be loaded.');
    }
  }

  protected backToList(): void {
    this.receipt.set(undefined);
    this.returnable.set(undefined);
  }

  protected async startReturn(sale: Sale): Promise<void> {
    if (!this.canReadReturns()) {
      return;
    }
    this.returnNotice.set(undefined);
    this.returnLoading.set(true);
    try {
      const data = await firstValueFrom(this.returns.getReturnable(sale.id));
      this.returnable.set(data);
      this.returnQty = {};
      for (const item of data.items) {
        this.returnQty[item.saleItemId] = 0;
      }
      this.returnReason = '';
      this.returnRestock = true;
      this.returnMethod = 'Cash';
      this.returnReference = '';
    } catch {
      this.returnNotice.set('The returnable quantities could not be loaded.');
    } finally {
      this.returnLoading.set(false);
    }
  }

  protected cancelReturn(): void {
    this.returnable.set(undefined);
    this.returnNotice.set(undefined);
  }

  protected estimatedRefund(): number {
    const data = this.returnable();
    if (!data) {
      return 0;
    }
    return data.items.reduce(
      (sum, item) => sum + (this.returnQty[item.saleItemId] || 0) * item.netPerUnit,
      0,
    );
  }

  protected async submitReturn(): Promise<void> {
    const data = this.returnable();
    if (!data) {
      return;
    }
    this.returnNotice.set(undefined);

    const items = data.items
      .filter((item) => (this.returnQty[item.saleItemId] || 0) > 0)
      .map((item) => ({
        saleItemId: item.saleItemId,
        quantity: this.returnQty[item.saleItemId],
      }));

    if (items.length === 0) {
      this.returnNotice.set('Enter a return quantity for at least one line.');
      return;
    }
    if (!this.returnReason.trim()) {
      this.returnNotice.set('A return reason is required.');
      return;
    }
    if (this.returnMethod !== 'Cash' && !this.returnReference.trim()) {
      this.returnNotice.set('A transaction reference is required for non-cash refunds.');
      return;
    }

    const input: ProcessReturnInput = {
      saleId: data.saleId,
      items,
      reason: this.returnReason.trim(),
      restock: this.returnRestock,
      refundMethod: this.returnMethod,
      refundReference: this.returnReference.trim() || null,
      idempotencyKey: newIdempotencyKey(),
    };

    this.returnSaving.set(true);
    try {
      await firstValueFrom(this.returns.processReturn(input));
      this.returnable.set(await firstValueFrom(this.returns.getReturnable(data.saleId)));
      this.returnQty = {};
      for (const item of this.returnable()!.items) {
        this.returnQty[item.saleItemId] = 0;
      }
      this.returnReason = '';
      this.returnReference = '';
      this.returnNotice.set('Return processed. Refund was computed server-side.');
    } catch (error) {
      this.returnNotice.set(this.describeReturn(error, 'The return could not be processed.'));
    } finally {
      this.returnSaving.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(undefined);
    this.forbidden.set(false);

    try {
      this.sales.set(await firstValueFrom(this.api.listSales()));
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.forbidden.set(true);
      } else {
        this.error.set('Sales history could not be loaded.');
      }
    } finally {
      this.loading.set(false);
    }
  }

  private async reload(): Promise<void> {
    this.error.set(undefined);
    try {
      this.sales.set(
        await firstValueFrom(
          this.api.listSales(this.from || undefined, this.to || undefined),
        ),
      );
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.forbidden.set(true);
      } else {
        this.error.set('Sales history could not be reloaded.');
      }
    }
  }

  private describeReturn(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      const body = error.error as { detail?: string } | undefined;
      if (typeof body?.detail === 'string' && body.detail.length > 0) {
        return body.detail;
      }
      if (error.status === 409) {
        return 'Stock changed while processing the return. Reload and retry.';
      }
    }
    return fallback;
  }
}
