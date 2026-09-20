import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { PlatformApi, AuditLogEntry } from '../../core/http/platform-api.service';
import { LDateTimePipe, TranslatePipe } from '../../core/i18n/i18n.pipes';
import { LanguageService } from '../../core/i18n/language.service';

@Component({
  selector: 'app-audit-log',
  imports: [TranslatePipe, LDateTimePipe],
  templateUrl: './audit-log.html',
  styleUrl: './audit-log.css',
})
export class AuditLogPage implements OnInit {
  private readonly api = inject(PlatformApi);
  protected readonly lang = inject(LanguageService);

  protected readonly entries = signal<readonly AuditLogEntry[]>([]);
  protected readonly loading = signal(true);
  protected readonly forbidden = signal(false);
  protected readonly error = signal<string | undefined>(undefined);

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(undefined);
    this.forbidden.set(false);

    try {
      this.entries.set(await firstValueFrom(this.api.auditLogs()));
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.forbidden.set(true);
      } else {
        this.error.set(this.lang.t('audit.loadFailed'));
      }
    } finally {
      this.loading.set(false);
    }
  }
}
