import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { PERMISSIONS } from '../../core/auth/permissions';
import {
  AlertsSummary,
  Batch,
  InventoryApi,
  MedicineStock,
  Movement,
  ReceiveStockInput,
} from '../../core/http/inventory-api.service';
import { SessionService } from '../../core/session/session.service';

@Component({
  selector: 'app-inventory',
  imports: [FormsModule, DatePipe],
  templateUrl: './inventory.html',
  styleUrl: './inventory.css',
})
export class InventoryPage implements OnInit, OnDestroy {
  private readonly api = inject(InventoryApi);
  protected readonly session = inject(SessionService);

  protected readonly canManage = PERMISSIONS.tenantInventoryManage;

  protected readonly summary = signal<AlertsSummary | undefined>(undefined);
  protected readonly stock = signal<readonly MedicineStock[]>([]);
  protected readonly batches = signal<readonly Batch[]>([]);
  protected readonly movements = signal<readonly Movement[]>([]);
  protected readonly loading = signal(true);
  protected readonly forbidden = signal(false);
  protected readonly error = signal<string | undefined>(undefined);
  protected readonly notice = signal<string | undefined>(undefined);
  protected readonly saving = signal(false);

  protected search = '';
  protected lowStockOnly = false;
  protected selectedMedicineId: string | undefined;
  protected adjustingBatchId: string | undefined;

  protected receive: ReceiveStockInput = InventoryPage.blankReceive();
  protected adjustQty: number | null = null;
  protected adjustReason = '';
  private debounce: ReturnType<typeof setTimeout> | undefined;

  ngOnInit(): void {
    void this.load();
  }

  ngOnDestroy(): void {
    if (this.debounce) {
      clearTimeout(this.debounce);
    }
  }

  protected hasPermission(permission: string): boolean {
    return this.session.hasPermission(permission);
  }

  protected selectedMedicine(): MedicineStock | undefined {
    return this.stock().find((row) => row.medicineId === this.selectedMedicineId);
  }

  protected onSearchInput(): void {
    if (this.debounce) {
      clearTimeout(this.debounce);
    }
    this.debounce = setTimeout(() => void this.reloadStock(), 300);
  }

  protected onFilterChange(): void {
    void this.reloadStock();
  }

  protected async selectMedicine(id: string): Promise<void> {
    this.selectedMedicineId = id;
    this.adjustingBatchId = undefined;
    this.notice.set(undefined);
    this.receive = { ...InventoryPage.blankReceive(), medicineId: id };
    await Promise.all([this.reloadBatches(), this.reloadMovements()]);
  }

  protected startAdjust(batch: Batch): void {
    this.adjustingBatchId = batch.id;
    this.adjustQty = null;
    this.adjustReason = '';
    this.notice.set(undefined);
  }

  protected cancelAdjust(): void {
    this.adjustingBatchId = undefined;
  }

  protected async saveReceive(): Promise<void> {
    this.notice.set(undefined);

    const input = {
      ...this.receive,
      batchNumber: this.receive.batchNumber.trim(),
      supplierName: this.empty(this.receive.supplierName),
      purchaseReference: this.empty(this.receive.purchaseReference),
    };
    if (!input.medicineId || !input.batchNumber || !input.quantity || input.quantity < 1) {
      this.notice.set('Medicine, batch number and a positive quantity are required.');
      return;
    }
    if (!input.expiryDate) {
      this.notice.set('An expiry date is required. Expired stock can never be received.');
      return;
    }

    this.saving.set(true);
    try {
      await firstValueFrom(this.api.receiveStock(input));
      this.receive = { ...InventoryPage.blankReceive(), medicineId: input.medicineId };
      await this.refreshAll();
      this.notice.set('Stock received.');
    } catch (error) {
      this.notice.set(this.describe(error, 'The stock could not be received.'));
    } finally {
      this.saving.set(false);
    }
  }

  protected async saveAdjust(batch: Batch): Promise<void> {
    this.notice.set(undefined);

    if (this.adjustQty === null || this.adjustQty === 0 || !Number.isInteger(this.adjustQty)) {
      this.notice.set('Enter a non-zero whole-unit quantity change.');
      return;
    }

    this.saving.set(true);
    try {
      await firstValueFrom(
        this.api.adjustStock(batch.id, this.adjustQty, this.empty(this.adjustReason)),
      );
      this.adjustingBatchId = undefined;
      await this.refreshAll();
      this.notice.set('Adjustment recorded.');
    } catch (error) {
      this.notice.set(this.describe(error, 'The adjustment could not be recorded.'));
    } finally {
      this.saving.set(false);
    }
  }

  protected async expire(batch: Batch): Promise<void> {
    this.notice.set(undefined);
    this.saving.set(true);
    try {
      await firstValueFrom(this.api.expireBatch(batch.id));
      this.adjustingBatchId = undefined;
      await this.refreshAll();
      this.notice.set(`Batch ${batch.batchNumber} expired. ${batch.quantity} units removed.`);
    } catch (error) {
      this.notice.set(this.describe(error, 'The batch could not be expired.'));
    } finally {
      this.saving.set(false);
    }
  }

  private static blankReceive(): ReceiveStockInput {
    return {
      medicineId: '',
      batchNumber: '',
      quantity: 0,
      purchasePrice: null,
      sellingPrice: null,
      manufactureDate: null,
      expiryDate: '',
      supplierName: null,
      purchaseReference: null,
    };
  }

  private empty(value: string | null): string | null {
    return value === null || value.trim() === '' ? null : value.trim();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(undefined);
    this.forbidden.set(false);

    try {
      const [summary, stock] = await Promise.all([
        firstValueFrom(this.api.alertsSummary()),
        firstValueFrom(this.api.listStock()),
      ]);
      this.summary.set(summary);
      this.stock.set(stock);
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.forbidden.set(true);
      } else {
        this.error.set('The inventory could not be loaded.');
      }
    } finally {
      this.loading.set(false);
    }
  }

  private async refreshAll(): Promise<void> {
    const [summary, stock] = await Promise.all([
      firstValueFrom(this.api.alertsSummary()),
      firstValueFrom(
        this.api.listStock(this.search.trim() || undefined, this.lowStockOnly),
      ),
    ]);
    this.summary.set(summary);
    this.stock.set(stock);
    if (this.selectedMedicineId) {
      await Promise.all([this.reloadBatches(), this.reloadMovements()]);
    }
  }

  private async reloadStock(): Promise<void> {
    this.error.set(undefined);
    try {
      this.stock.set(
        await firstValueFrom(
          this.api.listStock(this.search.trim() || undefined, this.lowStockOnly),
        ),
      );
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.forbidden.set(true);
      } else {
        this.error.set('The stock list could not be reloaded.');
      }
    }
  }

  private async reloadBatches(): Promise<void> {
    if (!this.selectedMedicineId) {
      this.batches.set([]);
      return;
    }
    this.batches.set(await firstValueFrom(this.api.listBatches(this.selectedMedicineId)));
  }

  private async reloadMovements(): Promise<void> {
    if (!this.selectedMedicineId) {
      this.movements.set([]);
      return;
    }
    this.movements.set(await firstValueFrom(this.api.listMovements(this.selectedMedicineId)));
  }

  private describe(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      const detail = (error.error as { detail?: string } | undefined)?.detail;
      if (typeof detail === 'string' && detail.length > 0) {
        return detail;
      }
      if (error.status === 409) {
        return 'This batch changed while your request was processed. Reload and retry.';
      }
      if (error.status === 422) {
        return 'The operation was refused by a stock rule (see details above).';
      }
    }
    return fallback;
  }
}
