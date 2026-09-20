import { Component, OnInit, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { PERMISSIONS } from './core/auth/permissions';
import { KeycloakService } from './core/auth/keycloak.service';
import { SessionService } from './core/session/session.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App implements OnInit {
  protected readonly keycloak = inject(KeycloakService);
  protected readonly session = inject(SessionService);
  protected readonly year = new Date().getFullYear();

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

  ngOnInit(): void {
    if (this.keycloak.isAuthenticated()) {
      void this.session.refresh();
    }
  }

  protected signIn(): void {
    void this.keycloak.login();
  }

  protected signOut(): void {
    this.session.clear();
    void this.keycloak.logout();
  }
}
