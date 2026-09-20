import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AppConfigService } from '../config/app-config.service';

export interface ReturnItem {
  readonly id: string;
  readonly saleItemId: string;
  readonly medicineId: string;
  readonly medicineName: string;
  readonly batchId: string;
  readonly batchNumber: string;
  readonly quantity: number;
  readonly unitPrice: number;
  readonly refundAmount: number;
}

export interface SaleReturn {
  readonly id: string;
  readonly saleId: string;
  readonly invoiceNumber: string;
  readonly reason: string;
  readonly restock: boolean;
  readonly refundMethod: string;
  readonly refundReference: string | null;
  readonly refundAmount: number;
  readonly occurredAtUtc: string;
  readonly actorSubject: string | null;
  readonly items: readonly ReturnItem[];
}

export interface ReturnableItem {
  readonly saleItemId: string;
  readonly medicineId: string;
  readonly medicineName: string;
  readonly batchId: string;
  readonly batchNumber: string;
  readonly soldQuantity: number;
  readonly alreadyReturnedQuantity: number;
  readonly returnableQuantity: number;
  readonly unitPrice: number;
  readonly netPerUnit: number;
}

export interface ReturnableSale {
  readonly saleId: string;
  readonly invoiceNumber: string;
  readonly totalAmount: number;
  readonly alreadyRefundedAmount: number;
  readonly items: readonly ReturnableItem[];
}

export interface ReturnItemInput {
  saleItemId: string;
  quantity: number;
}

export interface ProcessReturnInput {
  saleId: string;
  items: ReturnItemInput[];
  reason: string;
  restock: boolean;
  refundMethod: string;
  refundReference: string | null;
  idempotencyKey: string;
}

/**
 * Tenant sale-return endpoints. Reads require `tenant.returns.read`; processing requires
 * `tenant.returns.manage`. Refunds are computed server-side; the client sends quantities.
 */
@Injectable({ providedIn: 'root' })
export class ReturnsApi {
  private readonly http = inject(HttpClient);
  private readonly appConfig = inject(AppConfigService);

  private get baseUrl(): string {
    return `${this.appConfig.apiBaseUrl}/tenant/returns`;
  }

  processReturn(input: ProcessReturnInput): Observable<SaleReturn> {
    return this.http.post<SaleReturn>(`${this.baseUrl}`, input);
  }

  listReturns(saleId?: string): Observable<readonly SaleReturn[]> {
    return this.http.get<readonly SaleReturn[]>(`${this.baseUrl}`, {
      params: {
        ...(saleId ? { saleId } : {}),
        skip: '0',
        take: '50',
      },
    });
  }

  getReturn(id: string): Observable<SaleReturn> {
    return this.http.get<SaleReturn>(`${this.baseUrl}/${id}`);
  }

  getReturnable(saleId: string): Observable<ReturnableSale> {
    return this.http.get<ReturnableSale>(`${this.baseUrl}/sales/${saleId}/returnable`);
  }
}
