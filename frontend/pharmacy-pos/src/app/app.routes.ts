import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { permissionGuard } from './core/auth/permission.guard';
import { PERMISSIONS } from './core/auth/permissions';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  {
    path: 'dashboard',
    title: 'Dashboard · PharmaPOS',
    canActivate: [authGuard],
    loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard),
  },
  {
    path: 'audit',
    title: 'Audit log · PharmaPOS',
    canActivate: [permissionGuard(PERMISSIONS.platformAuditRead)],
    loadComponent: () => import('./features/audit/audit-log').then((m) => m.AuditLogPage),
  },
  {
    path: 'tenants',
    title: 'Tenants · PharmaPOS',
    canActivate: [permissionGuard(PERMISSIONS.platformTenantsManage)],
    loadComponent: () => import('./features/tenants/tenants').then((m) => m.TenantsPage),
  },
  {
    path: 'team',
    title: 'Team · PharmaPOS',
    canActivate: [permissionGuard(PERMISSIONS.tenantMembersManage)],
    loadComponent: () => import('./features/team/team').then((m) => m.TeamPage),
  },
  {
    path: 'roles',
    title: 'Roles · PharmaPOS',
    canActivate: [permissionGuard(PERMISSIONS.tenantRolesManage)],
    loadComponent: () => import('./features/roles/roles').then((m) => m.RolesPage),
  },
  {
    path: 'medicines',
    title: 'Medicines · PharmaPOS',
    canActivate: [permissionGuard(PERMISSIONS.tenantMedicinesRead)],
    loadComponent: () => import('./features/medicines/medicines').then((m) => m.MedicinesPage),
  },
  {
    path: 'catalog-data',
    title: 'Catalog data · PharmaPOS',
    canActivate: [permissionGuard(PERMISSIONS.tenantMedicinesManage)],
    loadComponent: () =>
      import('./features/catalog-data/catalog-data').then((m) => m.CatalogDataPage),
  },
  {
    path: 'pharmacy-profile',
    title: 'Pharmacy profile · PharmaPOS',
    canActivate: [permissionGuard(PERMISSIONS.tenantMedicinesRead)],
    loadComponent: () =>
      import('./features/pharmacy-profile/pharmacy-profile').then((m) => m.PharmacyProfilePage),
  },
  {
    path: 'inventory',
    title: 'Inventory · PharmaPOS',
    canActivate: [permissionGuard(PERMISSIONS.tenantInventoryRead)],
    loadComponent: () => import('./features/inventory/inventory').then((m) => m.InventoryPage),
  },
  {
    path: 'suppliers',
    title: 'Suppliers · PharmaPOS',
    canActivate: [permissionGuard(PERMISSIONS.tenantPurchasingRead)],
    loadComponent: () => import('./features/suppliers/suppliers').then((m) => m.SuppliersPage),
  },
  {
    path: 'purchases',
    title: 'Purchases · PharmaPOS',
    canActivate: [permissionGuard(PERMISSIONS.tenantPurchasingRead)],
    loadComponent: () => import('./features/purchases/purchases').then((m) => m.PurchasesPage),
  },
  {
    path: 'pos',
    title: 'Point of sale · PharmaPOS',
    canActivate: [permissionGuard(PERMISSIONS.tenantSalesManage)],
    loadComponent: () => import('./features/pos/pos').then((m) => m.PosPage),
  },
  {
    path: 'sales',
    title: 'Sales · PharmaPOS',
    canActivate: [permissionGuard(PERMISSIONS.tenantSalesRead)],
    loadComponent: () => import('./features/sales/sales').then((m) => m.SalesPage),
  },
  {
    path: 'customers',
    title: 'Customers · PharmaPOS',
    canActivate: [permissionGuard(PERMISSIONS.tenantCustomersRead)],
    loadComponent: () => import('./features/customers/customers').then((m) => m.CustomersPage),
  },
  {
    path: 'reports',
    title: 'Reports · PharmaPOS',
    canActivate: [authGuard],
    loadComponent: () => import('./features/reports/reports').then((m) => m.ReportsPage),
  },
  { path: '**', redirectTo: 'dashboard' },
];
