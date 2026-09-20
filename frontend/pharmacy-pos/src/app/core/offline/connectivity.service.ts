import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { timeout } from 'rxjs/operators';
import { PlatformApi } from '../http/platform-api.service';

const HEARTBEAT_TIMEOUT_MS = 5000;

/**
 * Online state for offline-safe POS behavior. `navigator.onLine` alone only reflects the
 * local network adapter, so transitions also trigger a cheap anonymous heartbeat
 * (`GET /platform/info`) before the UI treats the API as reachable.
 */
@Injectable({ providedIn: 'root' })
export class ConnectivityService {
  private readonly platform = inject(PlatformApi);

  private readonly state = signal<boolean>(
    typeof navigator === 'undefined' ? true : navigator.onLine,
  );

  readonly online = this.state.asReadonly();

  constructor() {
    try {
      window.addEventListener('online', () => void this.refresh());
      window.addEventListener('offline', () => this.state.set(false));
    } catch {
      // Non-DOM test environments: heartbeat callers drive the state instead.
    }
  }

  /**
   * Actively verifies API reachability: false when the browser reports offline,
   * otherwise the result of a timed heartbeat. Updates the shared signal.
   */
  async ensureOnline(): Promise<boolean> {
    if (typeof navigator !== 'undefined' && !navigator.onLine) {
      this.state.set(false);
      return false;
    }

    try {
      await firstValueFrom(this.platform.info().pipe(timeout(HEARTBEAT_TIMEOUT_MS)));
      this.state.set(true);
      return true;
    } catch {
      this.state.set(false);
      return false;
    }
  }

  /** Re-checks after browser online events (fire-and-forget is intended). */
  refresh(): Promise<boolean> {
    return this.ensureOnline();
  }

  /** Test seam: drive the signal without network. */
  setOnlineForTesting(value: boolean): void {
    this.state.set(value);
  }
}
