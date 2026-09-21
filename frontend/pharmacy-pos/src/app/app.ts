import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { PERMISSIONS } from './core/auth/permissions';
import { KeycloakService } from './core/auth/keycloak.service';
import { LanguageService } from './core/i18n/language.service';
import { TranslatePipe } from './core/i18n/i18n.pipes';
import { SessionService } from './core/session/session.service';
import { ThemeService } from './core/theme/theme.service';

const SIDEBAR_KEY = 'pharmapos.sidebar';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App implements OnInit {
  protected readonly keycloak = inject(KeycloakService);
  protected readonly session = inject(SessionService);
  protected readonly lang = inject(LanguageService);
  protected readonly theme = inject(ThemeService);
  private readonly router = inject(Router);
  protected readonly year = new Date().getFullYear();

  protected readonly sidebarCollapsed = signal(App.readSidebarCollapsed());
  protected readonly drawerOpen = signal(false);

  protected readonly canReadAudit = PERMISSIONS.platformAuditRead;
  protected readonly canManageTenants = PERMISSIONS.platformTenantsManage;
  protected readonly canManageMembers = PERMISSIONS.tenantMembersManage;
  protected readonly canManageRoles = PERMISSIONS.tenantRolesManage;
  protected readonly canReadMedicines = PERMISSIONS.tenantMedicinesRead;
  protected readonly canManageMedicines = PERMISSIONS.tenantMedicinesManage;
  protected readonly canReadInventory = PERMISSIONS.tenantInventoryRead;
  protected readonly canReadPurchasing = PERMISSIONS.tenantPurchasingRead;
  protected readonly canReadSales = PERMISSIONS.tenantSalesRead;
  protected readonly canManageSales = PERMISSIONS.tenantSalesManage;
  protected readonly canReadCustomers = PERMISSIONS.tenantCustomersRead;

  ngOnInit(): void {
    if (this.keycloak.isAuthenticated()) {
      void this.session.refresh();
    }
  }

  protected signIn(): void {
    void this.router.navigate(['/login']);
  }

  protected signOut(): void {
    this.session.clear();
    this.keycloak.logout();
    void this.router.navigate(['/login']);
  }

  protected switchLanguage(): void {
    this.lang.toggle();
  }

  protected toggleTheme(): void {
    this.theme.toggle();
  }

  protected toggleSidebar(): void {
    const collapsed = !this.sidebarCollapsed();
    this.sidebarCollapsed.set(collapsed);
    try {
      localStorage.setItem(SIDEBAR_KEY, collapsed ? '1' : '0');
    } catch {
      // Private-mode storage failures must never break the UI.
    }
  }

  protected toggleDrawer(): void {
    this.drawerOpen.update((open) => !open);
  }

  protected closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  protected navClicked(): void {
    this.closeDrawer();
  }

  private static readSidebarCollapsed(): boolean {
    try {
      return localStorage.getItem(SIDEBAR_KEY) === '1';
    } catch {
      return false;
    }
  }
}
