import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

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
    canActivate: [authGuard],
    loadComponent: () => import('./features/audit/audit-log').then((m) => m.AuditLogPage),
  },
  { path: '**', redirectTo: 'dashboard' },
];
