import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AppConfigService } from '../config/app-config.service';

export interface PlatformInfo {
  readonly name: string;
  readonly version: string;
  readonly environment: string;
  readonly serverTimeUtc: string;
  readonly status: string;
}

/**
 * Server-resolved session context (GET /platform/whoami). Identity, tenant, roles and
 * permissions come from the database membership graph — never from client-supplied values.
 */
export interface CurrentMembership {
  readonly subject: string | null;
  readonly username: string;
  readonly email: string;
  readonly tenantIdentifier: string;
  readonly tenantName: string;
  readonly userStatus: string;
  readonly membershipStatus: string;
  readonly isDefaultMembership: boolean;
  readonly roles: readonly string[];
  readonly permissions: readonly string[];
}

export interface AuditLogEntry {
  readonly id: string;
  readonly actorSubject: string | null;
  readonly action: string;
  readonly entityType: string | null;
  readonly entityId: string | null;
  readonly occurredAtUtc: string;
}

@Injectable({ providedIn: 'root' })
export class PlatformApi {
  private readonly http = inject(HttpClient);
  private readonly appConfig = inject(AppConfigService);

  private get baseUrl(): string {
    return `${this.appConfig.apiBaseUrl}/platform`;
  }

  info(): Observable<PlatformInfo> {
    return this.http.get<PlatformInfo>(`${this.baseUrl}/info`);
  }

  whoami(): Observable<CurrentMembership> {
    return this.http.get<CurrentMembership>(`${this.baseUrl}/whoami`);
  }

  auditLogs(take = 50): Observable<readonly AuditLogEntry[]> {
    return this.http.get<readonly AuditLogEntry[]>(`${this.baseUrl}/audit-logs`, {
      params: { take },
    });
  }
}
