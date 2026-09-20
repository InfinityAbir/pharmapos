import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AppConfigService } from '../config/app-config.service';

export interface Customer {
  readonly id: string;
  readonly name: string;
  readonly phone: string | null;
  readonly email: string | null;
  readonly address: string | null;
  readonly notes: string | null;
  readonly isActive: boolean;
}

export interface CustomerInput {
  name: string;
  phone: string | null;
  email: string | null;
  address: string | null;
  notes: string | null;
}

export interface CustomerHistoryItem {
  readonly saleId: string;
  readonly invoiceNumber: string;
  readonly occurredAtUtc: string;
  readonly totalAmount: number;
}

export interface CustomerWithHistory {
  readonly customer: Customer;
  readonly recentSales: readonly CustomerHistoryItem[];
}

/**
 * Tenant customers endpoints. Reads require `tenant.customers.read`; writes require
 * `tenant.customers.manage`. The API answers 403 otherwise.
 */
@Injectable({ providedIn: 'root' })
export class CustomersApi {
  private readonly http = inject(HttpClient);
  private readonly appConfig = inject(AppConfigService);

  private get baseUrl(): string {
    return `${this.appConfig.apiBaseUrl}/tenant/customers`;
  }

  listCustomers(search?: string): Observable<readonly Customer[]> {
    return this.http.get<readonly Customer[]>(`${this.baseUrl}`, {
      params: {
        ...(search ? { search } : {}),
        skip: '0',
        take: '50',
      },
    });
  }

  getCustomer(id: string): Observable<CustomerWithHistory> {
    return this.http.get<CustomerWithHistory>(`${this.baseUrl}/${id}`);
  }

  createCustomer(input: CustomerInput): Observable<Customer> {
    return this.http.post<Customer>(`${this.baseUrl}`, input);
  }

  updateCustomer(id: string, input: CustomerInput): Observable<Customer> {
    return this.http.put<Customer>(`${this.baseUrl}/${id}`, input);
  }

  setCustomerStatus(id: string, isActive: boolean): Observable<Customer> {
    return this.http.patch<Customer>(`${this.baseUrl}/${id}/status`, { isActive });
  }

  deleteCustomer(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
