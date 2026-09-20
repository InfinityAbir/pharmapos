import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AppConfigService } from '../config/app-config.service';

export interface SalesTrendBucket {
  readonly periodStart: string;
  readonly saleCount: number;
  readonly totalAmount: number;
}

export interface SalesByCashierRow {
  readonly cashier: string;
  readonly saleCount: number;
  readonly totalAmount: number;
}

export interface SalesByMethodRow {
  readonly method: string;
  readonly paymentCount: number;
  readonly totalAmount: number;
}

export interface SalesByMedicineRow {
  readonly medicineId: string;
  readonly medicineName: string;
  readonly quantitySold: number;
  readonly revenue: number;
}

export interface ProductPerformanceRow {
  readonly medicineId: string;
  readonly medicineName: string;
  readonly quantitySold: number;
  readonly revenue: number;
}

export interface StockValuation {
  readonly medicineCount: number;
  readonly totalUnits: number;
  readonly purchaseValue: number;
  readonly sellingValue: number;
  readonly rows: readonly StockValuationRow[];
}

export interface StockValuationRow {
  readonly medicineId: string;
  readonly medicineName: string;
  readonly totalQuantity: number;
  readonly purchaseValue: number;
  readonly sellingValue: number;
}

export interface LowStockRow {
  readonly medicineId: string;
  readonly medicineName: string;
  readonly totalQuantity: number;
  readonly reorderLevel: number;
  readonly deficit: number;
}

export interface PurchasesBySupplierRow {
  readonly supplierId: string;
  readonly supplierName: string;
  readonly purchaseCount: number;
  readonly totalAmount: number;
}

export interface PurchaseTotals {
  readonly from: string;
  readonly to: string;
  readonly receivedCount: number;
  readonly receivedTotal: number;
  readonly draftCount: number;
}

/**
 * Tenant report endpoints. Every call returns compact server-side aggregates —
 * the frontend renders them as-is and never computes totals, averages, or rankings
 * itself. Sales endpoints require `tenant.sales.read`, inventory `tenant.inventory.read`,
 * purchasing `tenant.purchasing.read`; the API answers 403 otherwise.
 */
@Injectable({ providedIn: 'root' })
export class ReportsApi {
  private readonly http = inject(HttpClient);
  private readonly appConfig = inject(AppConfigService);

  private get baseUrl(): string {
    return `${this.appConfig.apiBaseUrl}/tenant/reports`;
  }

  salesTrend(from?: string, to?: string, bucket = 'day'): Observable<readonly SalesTrendBucket[]> {
    return this.http.get<readonly SalesTrendBucket[]>(`${this.baseUrl}/sales/trend`, {
      params: {
        ...(from ? { from } : {}),
        ...(to ? { to } : {}),
        bucket,
      },
    });
  }

  salesByCashier(from?: string, to?: string): Observable<readonly SalesByCashierRow[]> {
    return this.http.get<readonly SalesByCashierRow[]>(`${this.baseUrl}/sales/by-cashier`, {
      params: { ...(from ? { from } : {}), ...(to ? { to } : {}), skip: '0', take: '100' },
    });
  }

  salesByMethod(from?: string, to?: string): Observable<readonly SalesByMethodRow[]> {
    return this.http.get<readonly SalesByMethodRow[]>(`${this.baseUrl}/sales/by-method`, {
      params: { ...(from ? { from } : {}), ...(to ? { to } : {}) },
    });
  }

  salesByMedicine(from?: string, to?: string): Observable<readonly SalesByMedicineRow[]> {
    return this.http.get<readonly SalesByMedicineRow[]>(`${this.baseUrl}/sales/by-medicine`, {
      params: { ...(from ? { from } : {}), ...(to ? { to } : {}), skip: '0', take: '100' },
    });
  }

  productPerformance(
    days = 30,
    bestFirst = true,
    take = 25,
  ): Observable<readonly ProductPerformanceRow[]> {
    return this.http.get<readonly ProductPerformanceRow[]>(
      `${this.baseUrl}/products/performance`,
      { params: { days: String(days), bestFirst: String(bestFirst), skip: '0', take: String(take) } },
    );
  }

  stockValuation(): Observable<StockValuation> {
    return this.http.get<StockValuation>(`${this.baseUrl}/inventory/valuation`, {
      params: { skip: '0', take: '100' },
    });
  }

  lowStockReport(): Observable<readonly LowStockRow[]> {
    return this.http.get<readonly LowStockRow[]>(`${this.baseUrl}/inventory/low-stock`, {
      params: { skip: '0', take: '100' },
    });
  }

  purchaseTotals(from?: string, to?: string): Observable<PurchaseTotals> {
    return this.http.get<PurchaseTotals>(`${this.baseUrl}/purchases/totals`, {
      params: { ...(from ? { from } : {}), ...(to ? { to } : {}) },
    });
  }

  purchasesBySupplier(from?: string, to?: string): Observable<readonly PurchasesBySupplierRow[]> {
    return this.http.get<readonly PurchasesBySupplierRow[]>(`${this.baseUrl}/purchases/by-supplier`, {
      params: { ...(from ? { from } : {}), ...(to ? { to } : {}), skip: '0', take: '100' },
    });
  }
}
