import { Component, OnInit, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { PERMISSIONS } from '../../core/auth/permissions';
import { KeycloakService } from '../../core/auth/keycloak.service';
import { CatalogApi, CatalogSummary } from '../../core/http/catalog-api.service';
import { AlertsSummary, InventoryApi } from '../../core/http/inventory-api.service';
import { PlatformApi, PlatformInfo } from '../../core/http/platform-api.service';
import { PurchasingApi, PurchasingSummary } from '../../core/http/purchasing-api.service';
import { ProductPerformanceRow, ReportsApi } from '../../core/http/reports-api.service';
import { Sale, SalesApi, TodaySales } from '../../core/http/sales-api.service';
import { LDateTimePipe, MoneyPipe, TranslatePipe } from '../../core/i18n/i18n.pipes';
import { LanguageService } from '../../core/i18n/language.service';
import { SessionService } from '../../core/session/session.service';

@Component({
  selector: 'app-dashboard',
  imports: [TranslatePipe, MoneyPipe, LDateTimePipe],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard implements OnInit {
  private readonly api = inject(PlatformApi);
  private readonly catalog = inject(CatalogApi);
  private readonly inventory = inject(InventoryApi);
  private readonly purchasing = inject(PurchasingApi);
  private readonly sales = inject(SalesApi);
  private readonly reports = inject(ReportsApi);
  private readonly keycloak = inject(KeycloakService);
  protected readonly session = inject(SessionService);
  protected readonly lang = inject(LanguageService);

  protected readonly isAuthenticated = this.keycloak.isAuthenticated;
  protected readonly info = signal<PlatformInfo | undefined>(undefined);
  protected readonly summary = signal<CatalogSummary | undefined>(undefined);
  protected readonly alerts = signal<AlertsSummary | undefined>(undefined);
  protected readonly purchasingSummary = signal<PurchasingSummary | undefined>(undefined);
  protected readonly todaySales = signal<TodaySales | undefined>(undefined);
  protected readonly topSellers = signal<readonly ProductPerformanceRow[]>([]);
  protected readonly recentSales = signal<readonly Sale[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | undefined>(undefined);

  ngOnInit(): void {
    void this.load();
  }

  protected signIn(): void {
    void this.keycloak.login();
  }

  protected canReadCatalog(): boolean {
    return this.session.hasPermission(PERMISSIONS.tenantMedicinesRead);
  }

  protected canReadInventory(): boolean {
    return this.session.hasPermission(PERMISSIONS.tenantInventoryRead);
  }

  protected canReadPurchasing(): boolean {
    return this.session.hasPermission(PERMISSIONS.tenantPurchasingRead);
  }

  protected canReadSales(): boolean {
    return this.session.hasPermission(PERMISSIONS.tenantSalesRead);
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(undefined);

    try {
      const info = await firstValueFrom(this.api.info());
      this.info.set(info);

      if (this.keycloak.isAuthenticated()) {
        if (!this.session.loaded()) {
          await this.session.refresh();
        }
        if (this.canReadCatalog()) {
          this.summary.set(await firstValueFrom(this.catalog.catalogSummary()));
        }
        if (this.canReadInventory()) {
          this.alerts.set(await firstValueFrom(this.inventory.alertsSummary()));
        }
        if (this.canReadPurchasing()) {
          this.purchasingSummary.set(
            await firstValueFrom(this.purchasing.purchasingSummary()),
          );
        }
        if (this.canReadSales()) {
          this.todaySales.set(await firstValueFrom(this.sales.todaySales()));
          this.topSellers.set(await firstValueFrom(this.reports.productPerformance(30, true, 5)));
          this.recentSales.set(await firstValueFrom(this.sales.listSales()));
        }
      }
    } catch {
      this.error.set(this.lang.t('dashboard.apiUnreachableError'));
    } finally {
      this.loading.set(false);
    }
  }
}
