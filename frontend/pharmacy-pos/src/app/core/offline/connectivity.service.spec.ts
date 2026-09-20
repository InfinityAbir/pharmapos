// @vitest-environment jsdom
import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { of, throwError } from 'rxjs';
import { PlatformApi, PlatformInfo } from '../http/platform-api.service';
import { ConnectivityService } from './connectivity.service';

beforeAll(() => {
  TestBed.configureTestingModule({});
});

describe('ConnectivityService', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.unstubAllGlobals();
  });

  function service(info: () => ReturnType<PlatformApi['info']>): ConnectivityService {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [ConnectivityService, { provide: PlatformApi, useValue: { info } }],
    });
    return TestBed.inject(ConnectivityService);
  }

  it('reports online after a successful heartbeat', async () => {
    const connectivity = service(() => of({} as PlatformInfo));

    await expect(connectivity.ensureOnline()).resolves.toBe(true);
    expect(connectivity.online()).toBe(true);
  });

  it('reports offline when the heartbeat fails', async () => {
    const connectivity = service(() =>
      throwError(() => new HttpErrorResponse({ status: 0, error: {} })),
    );

    await expect(connectivity.ensureOnline()).resolves.toBe(false);
    expect(connectivity.online()).toBe(false);
  });

  it('short-circuits when the browser reports offline', async () => {
    Object.defineProperty(window.navigator, 'onLine', { value: false, configurable: true });
    const info = vi.fn();
    const connectivity = service(info as never);

    try {
      await expect(connectivity.ensureOnline()).resolves.toBe(false);
    } finally {
      Object.defineProperty(window.navigator, 'onLine', { value: true, configurable: true });
    }
    expect(info).not.toHaveBeenCalled();
    expect(connectivity.online()).toBe(false);
  });
});
