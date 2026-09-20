import { Pipe, PipeTransform, inject } from '@angular/core';
import { LanguageService } from './language.service';

/** Template translation: `{{ 'nav.dashboard' | t }}`. Impure — re-renders on switch. */
@Pipe({ name: 't', standalone: true, pure: false })
export class TranslatePipe implements PipeTransform {
  private readonly lang = inject(LanguageService);

  transform(key: string, params?: Record<string, string | number>): string {
    return this.lang.t(key, params);
  }
}

/** Locale-aware BDT money: `{{ row.totalAmount | money }}`. */
@Pipe({ name: 'money', standalone: true, pure: false })
export class MoneyPipe implements PipeTransform {
  private readonly lang = inject(LanguageService);

  transform(value: number | null | undefined): string {
    return this.lang.formatMoney(value ?? 0);
  }
}

/** Locale-aware date: `{{ row.periodStart | ldate }}`. */
@Pipe({ name: 'ldate', standalone: true, pure: false })
export class LDatePipe implements PipeTransform {
  private readonly lang = inject(LanguageService);

  transform(value: string | Date | null | undefined): string {
    if (!value) {
      return '';
    }
    return this.lang.formatDate(value);
  }
}

/** Locale-aware date+time: `{{ sale.occurredAtUtc | ldatetime }}`. */
@Pipe({ name: 'ldatetime', standalone: true, pure: false })
export class LDateTimePipe implements PipeTransform {
  private readonly lang = inject(LanguageService);

  transform(value: string | Date | null | undefined): string {
    if (!value) {
      return '';
    }
    return this.lang.formatDateTime(value);
  }
}
