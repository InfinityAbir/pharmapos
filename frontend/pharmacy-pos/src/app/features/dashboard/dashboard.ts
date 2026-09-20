import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { PERMISSIONS } from '../../core/auth/permissions';
import { KeycloakService } from '../../core/auth/keycloak.service';
import { CatalogApi, CatalogSummary } from '../../core/http/catalog-api.service';
import { AlertsSummary, InventoryApi } from '../../core/http/inventory-api.service';
import { PlatformApi, PlatformInfo } from '../../core/http/platform-api.service';
import { PurchasingApi, PurchasingSummary } from '../../core/http/purchasing-api.service';
import { SalesApi, TodaySales } from '../../core/http/sales-api.service';
import { SessionService } from '../../core/session/session.service';

@Component({
  selector: 'app-dashboard',
  imports: [DatePipe],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard implements OnInit {
  private readonly api = inject(PlatformApi);
  private readonly catalog = inject(CatalogApi);
  private readonly inventory = inject(InventoryApi);
  private readonly purchasing = inject(PurchasingApi);
  private readonly sales = inject(SalesApi);
  private readonly keycloak = inject(KeycloakService);
  protected readonly session = inject(SessionService);

  protected readonly isAuthenticated = this.keycloak.isAuthenticated;
  protected readonly info = signal<PlatformInfo | undefined>(undefined);
  protected readonly summary = signal<CatalogSummary | undefined>(undefined);
  protected readonly alerts = signal<AlertsSummary | undefined>(undefined);
  protected readonly purchasingSummary = signal<PurchasingSummary | undefined>(undefined);
  protected readonly todaySales = signal<TodaySales | undefined>(undefined);
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
        }
      }
    } catch {
      this.error.set('The PharmaPOS API could not be reached.');
    } finally {
      this.loading.set(false);
    }
  }
}
