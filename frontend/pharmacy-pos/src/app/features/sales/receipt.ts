import { DatePipe } from '@angular/common';
import { Component, Input } from '@angular/core';
import { Receipt } from '../../core/http/sales-api.service';

@Component({
  selector: 'app-receipt',
  imports: [DatePipe],
  templateUrl: './receipt.html',
  styleUrl: './receipt.css',
})
export class ReceiptView {
  @Input({ required: true }) receipt!: Receipt;

  protected print(): void {
    window.print();
  }
}
