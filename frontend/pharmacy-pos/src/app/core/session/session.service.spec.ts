import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { KeycloakService } from '../auth/keycloak.service';
import { CurrentMembership, PlatformApi } from '../http/platform-api.service';
import { SessionService } from './session.service';

const context: CurrentMembership = {
  subject: 'subject-1',
  username: 'owner@demo-pharmacy.local',
  email: 'owner@demo-pharmacy.local',
  tenantIdentifier: 'demo-pharmacy',
  tenantName: 'Demo Pharmacy',
  userStatus: 'Active',
  membershipStatus: 'Active',
  isDefaultMembership: true,
  roles: ['Owner'],
  permissions: ['platform.audit.read', 'tenant.members.manage'],
};

function configure(authenticated: boolean, whoami: () => ReturnType<PlatformApi['whoami']>): SessionService {
  TestBed.configureTestingModule({
    providers: [
      SessionService,
      { provide: PlatformApi, useValue: { whoami } },
      {
        provide: KeycloakService,
        useValue: {
          isAuthenticated: signal(authenticated).asReadonly(),
          hasPermission: (permission: string) => context.permissions.includes(permission),
        },
      },
    ],
  });

  return TestBed.inject(SessionService);
}

describe('SessionService', () => {
  it('loads the server-resolved context and answers permission checks', async () => {
    const session = configure(true, () => of(context));

    await session.refresh();

    expect(session.loaded()).toBe(true);
    expect(session.context()).toEqual(context);
    expect(session.forbidden()).toBe(false);
    expect(session.hasPermission('platform.audit.read')).toBe(true);
    expect(session.hasPermission('platform.tenants.manage')).toBe(false);
  });

  it('marks forbidden when the caller has no active membership', async () => {
    const session = configure(
      true,
      () => throwError(() => new HttpErrorResponse({ status: 403 })),
    );

    await session.refresh();

    expect(session.loaded()).toBe(true);
    expect(session.context()).toBeUndefined();
    expect(session.forbidden()).toBe(true);
    // Visibility stays token-derived: realm roles still describe the caller even when this
    // tenant has no membership row for them (the API answers 403 on data calls).
    expect(session.hasPermission('platform.audit.read')).toBe(true);
  });

  it('reports an error when the session cannot be reached', async () => {
    const session = configure(
      true,
      () => throwError(() => new HttpErrorResponse({ status: 0 })),
    );

    await session.refresh();

    expect(session.loaded()).toBe(true);
    expect(session.forbidden()).toBe(false);
    expect(session.error()).toBeDefined();
  });

  it('stays empty for anonymous visitors without calling the API', async () => {
    let calls = 0;
    const session = configure(false, () => {
      calls += 1;
      return of(context);
    });

    await session.refresh();

    expect(calls).toBe(0);
    expect(session.context()).toBeUndefined();
    expect(session.loaded()).toBe(true);
  });

  it('clears the session on sign-out', async () => {
    const session = configure(true, () => of(context));
    await session.refresh();

    session.clear();

    expect(session.context()).toBeUndefined();
    expect(session.loaded()).toBe(false);
    expect(session.forbidden()).toBe(false);
  });
});
