import { DatePipe } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import {
  Customer,
  CustomerInput,
  CustomerWithHistory,
  CustomersApi,
} from '../../core/http/customers-api.service';

@Component({
  selector: 'app-customers',
  imports: [FormsModule, DatePipe],
  templateUrl: './customers.html',
  styleUrl: './customers.css',
})
export class CustomersPage implements OnInit, OnDestroy {
  private readonly api = inject(CustomersApi);

  protected readonly customers = signal<readonly Customer[]>([]);
  protected readonly detail = signal<CustomerWithHistory | undefined>(undefined);
  protected readonly loading = signal(true);
  protected readonly forbidden = signal(false);
  protected readonly error = signal<string | undefined>(undefined);
  protected readonly notice = signal<string | undefined>(undefined);
  protected readonly saving = signal(false);
  protected readonly editingId = signal<string | undefined>(undefined);

  protected search = '';
  protected form: CustomerInput = CustomersPage.blankForm();
  private debounce: ReturnType<typeof setTimeout> | undefined;

  ngOnInit(): void {
    void this.load();
  }

  ngOnDestroy(): void {
    if (this.debounce) {
      clearTimeout(this.debounce);
    }
  }

  protected onSearchInput(): void {
    if (this.debounce) {
      clearTimeout(this.debounce);
    }
    this.debounce = setTimeout(() => void this.reload(), 300);
  }

  protected startCreate(): void {
    this.editingId.set(undefined);
    this.form = CustomersPage.blankForm();
    this.notice.set(undefined);
  }

  protected startEdit(customer: Customer): void {
    this.editingId.set(customer.id);
    this.form = {
      name: customer.name,
      phone: customer.phone ?? '',
      email: customer.email ?? '',
      address: customer.address ?? '',
      notes: customer.notes ?? '',
    };
    this.notice.set(undefined);
  }

  protected async save(): Promise<void> {
    this.notice.set(undefined);

    const input = CustomersPage.normalise(this.form);
    if (!input.name) {
      this.notice.set('A customer name is required.');
      return;
    }

    this.saving.set(true);
    try {
      const editingId = this.editingId();
      if (editingId) {
        await firstValueFrom(this.api.updateCustomer(editingId, input));
      } else {
        await firstValueFrom(this.api.createCustomer(input));
      }
      this.startCreate();
      await this.reload();
    } catch (error) {
      this.notice.set(this.describe(error, 'The customer could not be saved.'));
    } finally {
      this.saving.set(false);
    }
  }

  protected async setStatus(customer: Customer, isActive: boolean): Promise<void> {
    this.notice.set(undefined);
    try {
      await firstValueFrom(this.api.setCustomerStatus(customer.id, isActive));
      await this.reload();
    } catch (error) {
      this.notice.set(this.describe(error, 'The customer status could not be changed.'));
    }
  }

  protected async remove(customer: Customer): Promise<void> {
    this.notice.set(undefined);
    try {
      await firstValueFrom(this.api.deleteCustomer(customer.id));
      if (this.editingId() === customer.id) {
        this.startCreate();
      }
      if (this.detail()?.customer.id === customer.id) {
        this.detail.set(undefined);
      }
      await this.reload();
    } catch (error) {
      this.notice.set(
        this.describe(error, 'The customer could not be deleted (linked sales block deletion).'),
      );
    }
  }

  protected async showHistory(customer: Customer): Promise<void> {
    this.error.set(undefined);
    try {
      this.detail.set(await firstValueFrom(this.api.getCustomer(customer.id)));
    } catch {
      this.error.set('The customer history could not be loaded.');
    }
  }

  protected backToList(): void {
    this.detail.set(undefined);
  }

  private static blankForm(): CustomerInput {
    return { name: '', phone: '', email: '', address: '', notes: '' };
  }

  private static normalise(form: CustomerInput): CustomerInput {
    const text = (value: string): string | null => {
      const trimmed = value.trim();
      return trimmed ? trimmed : null;
    };
    return {
      name: form.name.trim(),
      phone: text(form.phone ?? ''),
      email: text(form.email ?? ''),
      address: text(form.address ?? ''),
      notes: text(form.notes ?? ''),
    };
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(undefined);
    this.forbidden.set(false);

    try {
      this.customers.set(await firstValueFrom(this.api.listCustomers()));
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.forbidden.set(true);
      } else {
        this.error.set('Customers could not be loaded.');
      }
    } finally {
      this.loading.set(false);
    }
  }

  private async reload(): Promise<void> {
    this.error.set(undefined);
    try {
      this.customers.set(
        await firstValueFrom(this.api.listCustomers(this.search.trim() || undefined)),
      );
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.forbidden.set(true);
      } else {
        this.error.set('Customers could not be reloaded.');
      }
    }
  }

  private describe(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      const body = error.error as
        | { detail?: string; title?: string; code?: string }
        | undefined;
      if (typeof body?.detail === 'string' && body.detail.length > 0) {
        return body.detail;
      }
      if (error.status === 409) {
        return 'Another customer already uses this phone number.';
      }
    }
    return fallback;
  }
}
