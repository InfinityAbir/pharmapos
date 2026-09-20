import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AppConfigService } from '../config/app-config.service';

export interface Batch {
  readonly id: string;
  readonly medicineId: string;
  readonly medicineName: string;
  readonly batchNumber: string;
  readonly purchasePrice: number;
  readonly sellingPrice: number;
  readonly quantity: number;
  readonly manufactureDate: string | null;
  readonly expiryDate: string;
  readonly supplierName: string | null;
  readonly purchaseReference: string | null;
  readonly isExpired: boolean;
}

export interface ReceiveStockInput {
  medicineId: string;
  batchNumber: string;
  quantity: number;
  purchasePrice: number | null;
  sellingPrice: number | null;
  manufactureDate: string | null;
  expiryDate: string;
  supplierName: string | null;
  purchaseReference: string | null;
}

export interface MedicineStock {
  readonly medicineId: string;
  readonly brandName: string;
  readonly strength: string;
  readonly totalQuantity: number;
  readonly reorderLevel: number;
  readonly isLowStock: boolean;
  readonly batchCount: number;
  readonly batches: readonly Batch[];
}

export interface Movement {
  readonly id: string;
  readonly medicineId: string;
  readonly medicineName: string;
  readonly batchId: string;
  readonly batchNumber: string;
  readonly type: string;
  readonly quantityChange: number;
  readonly quantityAfter: number;
  readonly reason: string | null;
  readonly actorSubject: string | null;
  readonly occurredAtUtc: string;
}

export interface AlertsSummary {
  readonly lowStockCount: number;
  readonly expiringWithin30DaysCount: number;
  readonly expiredBatchCount: number;
}

/**
 * Tenant inventory endpoints. Reads require `tenant.inventory.read`; receipts, adjustments and
 * expiry require `tenant.inventory.manage`. The API answers 403 otherwise.
 */
@Injectable({ providedIn: 'root' })
export class InventoryApi {
  private readonly http = inject(HttpClient);
  private readonly appConfig = inject(AppConfigService);

  private get baseUrl(): string {
    return `${this.appConfig.apiBaseUrl}/tenant/inventory`;
  }

  listStock(search?: string, lowStockOnly = false): Observable<readonly MedicineStock[]> {
    return this.http.get<readonly MedicineStock[]>(`${this.baseUrl}/stock`, {
      params: {
        ...(search ? { search } : {}),
        lowStockOnly: String(lowStockOnly),
      },
    });
  }

  listLowStock(): Observable<readonly MedicineStock[]> {
    return this.http.get<readonly MedicineStock[]>(`${this.baseUrl}/stock/low-stock`);
  }

  listBatches(medicineId?: string): Observable<readonly Batch[]> {
    return this.http.get<readonly Batch[]>(`${this.baseUrl}/batches`, {
      params: { ...(medicineId ? { medicineId } : {}) },
    });
  }

  receiveStock(input: ReceiveStockInput): Observable<Batch> {
    return this.http.post<Batch>(`${this.baseUrl}/batches/receive`, input);
  }

  adjustStock(id: string, quantityChange: number, reason: string | null): Observable<Batch> {
    return this.http.post<Batch>(`${this.baseUrl}/batches/${id}/adjust`, {
      quantityChange,
      reason,
    });
  }

  expireBatch(id: string): Observable<Batch> {
    return this.http.post<Batch>(`${this.baseUrl}/batches/${id}/expire`, {});
  }

  listExpiring(withinDays = 30): Observable<readonly Batch[]> {
    return this.http.get<readonly Batch[]>(`${this.baseUrl}/expiring`, {
      params: { withinDays: String(withinDays) },
    });
  }

  listExpired(): Observable<readonly Batch[]> {
    return this.http.get<readonly Batch[]>(`${this.baseUrl}/expired`);
  }

  listMovements(
    medicineId?: string,
    batchId?: string,
    take = 50,
  ): Observable<readonly Movement[]> {
    return this.http.get<readonly Movement[]>(`${this.baseUrl}/movements`, {
      params: {
        ...(medicineId ? { medicineId } : {}),
        ...(batchId ? { batchId } : {}),
        skip: '0',
        take: String(take),
      },
    });
  }

  alertsSummary(): Observable<AlertsSummary> {
    return this.http.get<AlertsSummary>(`${this.baseUrl}/alerts/summary`);
  }
}
