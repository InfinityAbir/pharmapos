import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AppConfigService } from '../config/app-config.service';

export interface Supplier {
  readonly id: string;
  readonly name: string;
  readonly company: string | null;
  readonly phone: string | null;
  readonly email: string | null;
  readonly address: string | null;
  readonly contactPerson: string | null;
  readonly paymentTerms: string | null;
  readonly isActive: boolean;
}

export interface SupplierInput {
  name: string;
  company: string | null;
  phone: string | null;
  email: string | null;
  address: string | null;
  contactPerson: string | null;
  paymentTerms: string | null;
}

export interface PurchaseItem {
  readonly id: string;
  readonly medicineId: string;
  readonly medicineName: string;
  readonly batchNumber: string;
  readonly manufactureDate: string | null;
  readonly expiryDate: string;
  readonly quantity: number;
  readonly unitPrice: number;
  readonly discountAmount: number;
  readonly sellingPrice: number | null;
  readonly lineTotal: number;
}

export interface Purchase {
  readonly id: string;
  readonly supplierId: string;
  readonly supplierName: string;
  readonly purchaseDate: string;
  readonly status: string;
  readonly supplierInvoiceNumber: string | null;
  readonly subtotal: number;
  readonly discountAmount: number;
  readonly taxAmount: number;
  readonly totalAmount: number;
  readonly notes: string | null;
  readonly createdAtUtc: string;
  readonly receivedAtUtc: string | null;
  readonly items: readonly PurchaseItem[];
}

export interface PurchaseItemInput {
  medicineId: string;
  batchNumber: string;
  manufactureDate: string | null;
  expiryDate: string;
  quantity: number;
  unitPrice: number;
  discountAmount: number;
  sellingPrice: number | null;
}

export interface PurchaseInput {
  supplierId: string;
  purchaseDate: string;
  supplierInvoiceNumber: string | null;
  discountAmount: number;
  taxAmount: number;
  notes: string | null;
  items: PurchaseItemInput[];
}

export interface PurchasingSummary {
  readonly draftCount: number;
  readonly receivedThisMonthCount: number;
  readonly receivedThisMonthTotal: number;
}

/**
 * Tenant purchasing endpoints. Reads require `tenant.purchasing.read`; writes require
 * `tenant.purchasing.manage`. Receiving is idempotent per client-generated key.
 */
@Injectable({ providedIn: 'root' })
export class PurchasingApi {
  private readonly http = inject(HttpClient);
  private readonly appConfig = inject(AppConfigService);

  private get baseUrl(): string {
    return `${this.appConfig.apiBaseUrl}/tenant`;
  }

  listSuppliers(search?: string): Observable<readonly Supplier[]> {
    return this.http.get<readonly Supplier[]>(`${this.baseUrl}/suppliers`, {
      params: { ...(search ? { search } : {}) },
    });
  }

  createSupplier(input: SupplierInput): Observable<Supplier> {
    return this.http.post<Supplier>(`${this.baseUrl}/suppliers`, input);
  }

  updateSupplier(id: string, input: SupplierInput): Observable<Supplier> {
    return this.http.put<Supplier>(`${this.baseUrl}/suppliers/${id}`, input);
  }

  setSupplierStatus(id: string, isActive: boolean): Observable<Supplier> {
    return this.http.patch<Supplier>(`${this.baseUrl}/suppliers/${id}/status`, { isActive });
  }

  deleteSupplier(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/suppliers/${id}`);
  }

  listPurchases(supplierId?: string, status?: string): Observable<readonly Purchase[]> {
    return this.http.get<readonly Purchase[]>(`${this.baseUrl}/purchases`, {
      params: {
        ...(supplierId ? { supplierId } : {}),
        ...(status ? { status } : {}),
        skip: '0',
        take: '50',
      },
    });
  }

  getPurchase(id: string): Observable<Purchase> {
    return this.http.get<Purchase>(`${this.baseUrl}/purchases/${id}`);
  }

  createDraft(input: PurchaseInput): Observable<Purchase> {
    return this.http.post<Purchase>(`${this.baseUrl}/purchases`, input);
  }

  updateDraft(id: string, input: PurchaseInput): Observable<Purchase> {
    return this.http.put<Purchase>(`${this.baseUrl}/purchases/${id}`, input);
  }

  receivePurchase(id: string, idempotencyKey: string): Observable<Purchase> {
    return this.http.post<Purchase>(`${this.baseUrl}/purchases/${id}/receive`, {
      idempotencyKey,
    });
  }

  cancelPurchase(id: string): Observable<Purchase> {
    return this.http.post<Purchase>(`${this.baseUrl}/purchases/${id}/cancel`, {});
  }

  purchasingSummary(): Observable<PurchasingSummary> {
    return this.http.get<PurchasingSummary>(`${this.baseUrl}/purchasing/summary`);
  }
}
