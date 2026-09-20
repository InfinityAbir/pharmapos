import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Receipt, Sale, SalesApi } from '../../core/http/sales-api.service';
import { ReceiptView } from './receipt';

@Component({
  selector: 'app-sales',
  imports: [FormsModule, DatePipe, ReceiptView],
  templateUrl: './sales.html',
  styleUrl: './sales.css',
})
export class SalesPage implements OnInit {
  private readonly api = inject(SalesApi);

  protected readonly sales = signal<readonly Sale[]>([]);
  protected readonly receipt = signal<Receipt | undefined>(undefined);
  protected readonly loading = signal(true);
  protected readonly forbidden = signal(false);
  protected readonly error = signal<string | undefined>(undefined);

  protected from = '';
  protected to = '';

  ngOnInit(): void {
    void this.load();
  }

  protected onFilterChange(): void {
    void this.reload();
  }

  protected async showReceipt(sale: Sale): Promise<void> {
    this.error.set(undefined);
    try {
      this.receipt.set(await firstValueFrom(this.api.getReceipt(sale.id)));
    } catch {
      this.error.set('The receipt could not be loaded.');
    }
  }

  protected backToList(): void {
    this.receipt.set(undefined);
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(undefined);
    this.forbidden.set(false);

    try {
      this.sales.set(await firstValueFrom(this.api.listSales()));
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.forbidden.set(true);
      } else {
        this.error.set('Sales history could not be loaded.');
      }
    } finally {
      this.loading.set(false);
    }
  }

  private async reload(): Promise<void> {
    this.error.set(undefined);
    try {
      this.sales.set(
        await firstValueFrom(
          this.api.listSales(this.from || undefined, this.to || undefined),
        ),
      );
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.forbidden.set(true);
      } else {
        this.error.set('Sales history could not be reloaded.');
      }
    }
  }
}
