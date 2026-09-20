import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { PERMISSIONS } from '../../core/auth/permissions';
import {
  LowStockRow,
  ProductPerformanceRow,
  PurchaseTotals,
  PurchasesBySupplierRow,
  ReportsApi,
  SalesByCashierRow,
  SalesByMedicineRow,
  SalesByMethodRow,
  SalesTrendBucket,
  StockValuation,
} from '../../core/http/reports-api.service';
import { downloadCsv, toCsvRecords } from '../../core/http/csv-export';
import { LDatePipe, MoneyPipe, TranslatePipe } from '../../core/i18n/i18n.pipes';
import { LanguageService } from '../../core/i18n/language.service';
import { SessionService } from '../../core/session/session.service';

@Component({
  selector: 'app-reports',
  imports: [FormsModule, TranslatePipe, MoneyPipe, LDatePipe],
  templateUrl: './reports.html',
  styleUrl: './reports.css',
})
export class ReportsPage implements OnInit {
  private readonly api = inject(ReportsApi);
  protected readonly session = inject(SessionService);
  protected readonly lang = inject(LanguageService);

  protected readonly trend = signal<readonly SalesTrendBucket[]>([]);
  protected readonly byCashier = signal<readonly SalesByCashierRow[]>([]);
  protected readonly byMethod = signal<readonly SalesByMethodRow[]>([]);
  protected readonly byMedicine = signal<readonly SalesByMedicineRow[]>([]);
  protected readonly bestSellers = signal<readonly ProductPerformanceRow[]>([]);
  protected readonly slowMovers = signal<readonly ProductPerformanceRow[]>([]);
  protected readonly valuation = signal<StockValuation | undefined>(undefined);
  protected readonly lowStock = signal<readonly LowStockRow[]>([]);
  protected readonly purchaseTotals = signal<PurchaseTotals | undefined>(undefined);
  protected readonly bySupplier = signal<readonly PurchasesBySupplierRow[]>([]);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | undefined>(undefined);

  protected from = '';
  protected to = '';
  protected bucket = 'day';
  protected windowDays: number | null = 30;

  ngOnInit(): void {
    void this.load();
  }

  protected canReadSales(): boolean {
    return this.session.hasPermission(PERMISSIONS.tenantSalesRead);
  }

  protected canReadInventory(): boolean {
    return this.session.hasPermission(PERMISSIONS.tenantInventoryRead);
  }

  protected canReadPurchasing(): boolean {
    return this.session.hasPermission(PERMISSIONS.tenantPurchasingRead);
  }

  protected hasAnyReportPermission(): boolean {
    return this.canReadSales() || this.canReadInventory() || this.canReadPurchasing();
  }

  protected onFilterChange(): void {
    void this.load();
  }

  protected bucketLabel(): string {
    switch (this.bucket) {
      case 'week':
        return this.lang.t('reports.weekly');
      case 'month':
        return this.lang.t('reports.monthly');
      default:
        return this.lang.t('reports.daily');
    }
  }

  protected exportCsv(name: string, columns: readonly string[], rows: readonly unknown[]): void {
    downloadCsv(`${name}.csv`, toCsvRecords(columns, rows as readonly Record<string, unknown>[]));
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(undefined);

    const from = this.from || undefined;
    const to = this.to || undefined;

    try {
      if (this.canReadSales()) {
        const [trend, cashier, method, medicine, best, slow] = await Promise.all([
          firstValueFrom(this.api.salesTrend(from, to, this.bucket)),
          firstValueFrom(this.api.salesByCashier(from, to)),
          firstValueFrom(this.api.salesByMethod(from, to)),
          firstValueFrom(this.api.salesByMedicine(from, to)),
          firstValueFrom(this.api.productPerformance(this.windowDays || 30, true, 10)),
          firstValueFrom(this.api.productPerformance(this.windowDays || 30, false, 10)),
        ]);
        this.trend.set(trend);
        this.byCashier.set(cashier);
        this.byMethod.set(method);
        this.byMedicine.set(medicine);
        this.bestSellers.set(best);
        this.slowMovers.set(slow);
      }
      if (this.canReadInventory()) {
        const [valuation, low] = await Promise.all([
          firstValueFrom(this.api.stockValuation()),
          firstValueFrom(this.api.lowStockReport()),
        ]);
        this.valuation.set(valuation);
        this.lowStock.set(low);
      }
      if (this.canReadPurchasing()) {
        const [totals, supplier] = await Promise.all([
          firstValueFrom(this.api.purchaseTotals(from, to)),
          firstValueFrom(this.api.purchasesBySupplier(from, to)),
        ]);
        this.purchaseTotals.set(totals);
        this.bySupplier.set(supplier);
      }
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.error.set(this.lang.t('reports.permissionMissing'));
      } else {
        this.error.set(this.lang.t('reports.loadFailed'));
      }
    } finally {
      this.loading.set(false);
    }
  }
}
