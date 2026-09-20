import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AppConfigService } from '../config/app-config.service';
import { PharmacyProfile } from './catalog-api.service';

export interface SaleItem {
  readonly id: string;
  readonly medicineId: string;
  readonly medicineName: string;
  readonly batchId: string;
  readonly batchNumber: string;
  readonly quantity: number;
  readonly unitPrice: number;
  readonly discountAmount: number;
  readonly lineTotal: number;
}

export interface SalePayment {
  readonly id: string;
  readonly method: string;
  readonly amount: number;
  readonly reference: string | null;
}

export interface Sale {
  readonly id: string;
  readonly invoiceNumber: string;
  readonly occurredAtUtc: string;
  readonly subtotal: number;
  readonly discountAmount: number;
  readonly totalAmount: number;
  readonly paidAmount: number;
  readonly changeDue: number;
  readonly customerId: string | null;
  readonly customerName: string | null;
  readonly customerPhone: string | null;
  readonly prescriptionReference: string | null;
  readonly cashierSubject: string | null;
  readonly items: readonly SaleItem[];
  readonly payments: readonly SalePayment[];
}

export interface Receipt {
  readonly pharmacy: PharmacyProfile;
  readonly sale: Sale;
}

export interface TodaySales {
  readonly date: string;
  readonly saleCount: number;
  readonly totalAmount: number;
}

export interface CheckoutItemInput {
  medicineId: string;
  quantity: number;
  batchId: string | null;
  discountAmount: number;
}

export interface CheckoutPaymentInput {
  method: string;
  amount: number;
  reference: string | null;
}

export interface CheckoutInput {
  items: CheckoutItemInput[];
  payments: CheckoutPaymentInput[];
  discountAmount: number;
  customerId: string | null;
  customerName: string | null;
  customerPhone: string | null;
  prescriptionReference: string | null;
  idempotencyKey: string;
}

/** V1 payment methods (manual recording; no gateway claims). */
export const PAYMENT_METHODS: readonly string[] = ['Cash', 'Card', 'bKash', 'Nagad', 'Other'];

/**
 * Tenant sales endpoints. Checkout requires `tenant.sales.manage`; history and receipts
 * require `tenant.sales.read`. The API answers 403 otherwise.
 */
@Injectable({ providedIn: 'root' })
export class SalesApi {
  private readonly http = inject(HttpClient);
  private readonly appConfig = inject(AppConfigService);

  private get baseUrl(): string {
    return `${this.appConfig.apiBaseUrl}/tenant/sales`;
  }

  checkout(input: CheckoutInput): Observable<Sale> {
    return this.http.post<Sale>(`${this.baseUrl}/checkout`, input);
  }

  listSales(from?: string, to?: string): Observable<readonly Sale[]> {
    return this.http.get<readonly Sale[]>(`${this.baseUrl}`, {
      params: {
        ...(from ? { from } : {}),
        ...(to ? { to } : {}),
        skip: '0',
        take: '50',
      },
    });
  }

  getReceipt(id: string): Observable<Receipt> {
    return this.http.get<Receipt>(`${this.baseUrl}/${id}/receipt`);
  }

  todaySales(): Observable<TodaySales> {
    return this.http.get<TodaySales>(`${this.baseUrl}/summary/today`);
  }
}
