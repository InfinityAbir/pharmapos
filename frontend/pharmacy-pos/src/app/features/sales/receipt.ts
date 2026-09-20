import { Component, Input, inject } from '@angular/core';
import { Receipt } from '../../core/http/sales-api.service';
import { LDateTimePipe, MoneyPipe, TranslatePipe } from '../../core/i18n/i18n.pipes';
import { LanguageService } from '../../core/i18n/language.service';

@Component({
  selector: 'app-receipt',
  imports: [TranslatePipe, MoneyPipe, LDateTimePipe],
  templateUrl: './receipt.html',
  styleUrl: './receipt.css',
})
export class ReceiptView {
  @Input({ required: true }) receipt!: Receipt;

  protected readonly lang = inject(LanguageService);

  protected print(): void {
    window.print();
  }
}
