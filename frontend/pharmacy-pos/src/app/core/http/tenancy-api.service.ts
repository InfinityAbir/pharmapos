import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AppConfigService } from '../config/app-config.service';

export interface Tenant {
  readonly id: string;
  readonly identifier: string;
  readonly name: string;
  readonly status: string;
  readonly planCode: string | null;
}

export interface Role {
  readonly id: string;
  readonly name: string;
  readonly description: string | null;
  readonly isSystem: boolean;
  readonly permissions: readonly string[];
}

export interface Member {
  readonly membershipId: string;
  readonly status: string;
  readonly isDefault: boolean;
  readonly userId: string;
  readonly email: string;
  readonly displayName: string;
  readonly userActive: boolean;
  readonly roles: readonly string[];
}

/**
 * Tenant and platform administration endpoints. Every call requires the matching server-side
 * permission; the API answers 403 otherwise.
 */
@Injectable({ providedIn: 'root' })
export class TenancyApi {
  private readonly http = inject(HttpClient);
  private readonly appConfig = inject(AppConfigService);

  private get baseUrl(): string {
    return this.appConfig.apiBaseUrl;
  }

  listTenants(): Observable<readonly Tenant[]> {
    return this.http.get<readonly Tenant[]>(`${this.baseUrl}/platform/tenants`);
  }

  createTenant(identifier: string, name: string): Observable<Tenant> {
    return this.http.post<Tenant>(`${this.baseUrl}/platform/tenants`, { identifier, name });
  }

  setTenantStatus(identifier: string, status: string): Observable<Tenant> {
    return this.http.patch<Tenant>(`${this.baseUrl}/platform/tenants/${identifier}/status`, {
      status,
    });
  }

  listRoles(): Observable<readonly Role[]> {
    return this.http.get<readonly Role[]>(`${this.baseUrl}/tenant/roles`);
  }

  createRole(name: string, description: string | null, permissions: readonly string[]): Observable<Role> {
    return this.http.post<Role>(`${this.baseUrl}/tenant/roles`, {
      name,
      description,
      permissions,
    });
  }

  updateRole(
    id: string,
    name: string,
    description: string | null,
    permissions: readonly string[],
  ): Observable<Role> {
    return this.http.put<Role>(`${this.baseUrl}/tenant/roles/${id}`, {
      name,
      description,
      permissions,
    });
  }

  deleteRole(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/tenant/roles/${id}`);
  }

  listMembers(): Observable<readonly Member[]> {
    return this.http.get<readonly Member[]>(`${this.baseUrl}/tenant/members`);
  }

  setMembershipStatus(membershipId: string, status: string): Observable<Member> {
    return this.http.patch<Member>(`${this.baseUrl}/tenant/members/${membershipId}/status`, {
      status,
    });
  }

  replaceMembershipRoles(membershipId: string, roleIds: readonly string[]): Observable<Member> {
    return this.http.put<Member>(`${this.baseUrl}/tenant/members/${membershipId}/roles`, {
      roleIds,
    });
  }
}
