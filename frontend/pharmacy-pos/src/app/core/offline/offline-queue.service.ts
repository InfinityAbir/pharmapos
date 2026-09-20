import { Injectable, computed, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom, type Observable } from 'rxjs';
import type { CheckoutInput, Sale } from '../http/sales-api.service';

export type QueuedOpStatus = 'queued' | 'sending' | 'failed' | 'confirmed';

export interface ApiFailure {
  readonly status: number;
  readonly code?: string;
  readonly detail?: string;
}

/** One checkout intent captured while offline (or failed to send). */
export interface QueuedCheckout {
  readonly id: string;
  readonly tenantId: string;
  readonly input: CheckoutInput;
  readonly createdAt: string;
  status: QueuedOpStatus;
  attempts: number;
  failure?: ApiFailure;
  saleId?: string;
  invoiceNumber?: string;
}

const STORAGE_KEY = 'pharmapos.offlineQueue.v1';
const MAX_STORED = 50;

function newId(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID();
  }
  return `q-${Date.now()}-${Math.floor(Math.random() * 1_000_000_000)}`;
}

/**
 * Durable FIFO queue for checkout intents. Entries carry the cart's original idempotency
 * key, so replays and retries are deduplicated server-side: a repeated send returns the
 * stored sale instead of charging twice. Failures (422/409: stock changed, underpaid
 * against live prices) stay visible with the server reason for manual resolution —
 * entries are never auto-dropped and success is never faked.
 */
@Injectable({ providedIn: 'root' })
export class OfflineQueueService {
  private readonly entries = signal<readonly QueuedCheckout[]>(OfflineQueueService.readStored());
  private readonly syncing = signal(false);

  readonly items = this.entries.asReadonly();
  readonly isSyncing = this.syncing.asReadonly();
  readonly pendingCount = computed(
    () => this.entries().filter((entry) => entry.status === 'queued' || entry.status === 'failed').length,
  );

  enqueue(tenantId: string, input: CheckoutInput): QueuedCheckout {
    const entry: QueuedCheckout = {
      id: newId(),
      tenantId,
      input,
      createdAt: new Date().toISOString(),
      status: 'queued',
      attempts: 0,
    };
    this.update([...this.entries(), entry].slice(-MAX_STORED));
    return entry;
  }

  markRetrying(id: string): void {
    this.patch(id, (entry) => ({ ...entry, status: 'queued', failure: undefined }));
  }

  remove(id: string): void {
    this.update(this.entries().filter((entry) => entry.id !== id));
  }

  clearConfirmed(): void {
    this.update(this.entries().filter((entry) => entry.status !== 'confirmed'));
  }

  pendingForTenant(tenantId: string): readonly QueuedCheckout[] {
    return this.entries().filter(
      (entry) =>
        entry.tenantId === tenantId && (entry.status === 'queued' || entry.status === 'failed'),
    );
  }

  /**
   * Replays pending entries for one tenant, oldest first. Success (fresh or idempotent
   * replay — the server treats both identically) confirms the entry; API failures park
   * it with the server reason. Stops at the first transport failure (status 0) since the
   * connection is presumably gone again; business rejections (4xx) continue with the rest.
   */
  async syncAll(
    tenantId: string,
    sender: (input: CheckoutInput) => Observable<Sale>,
  ): Promise<void> {
    if (this.syncing()) {
      return;
    }
    const pending = this.pendingForTenant(tenantId);
    if (pending.length === 0) {
      return;
    }

    this.syncing.set(true);
    try {
      for (const entry of pending) {
        // Re-read per iteration: entries may change between sends.
        const current = this.entries().find((candidate) => candidate.id === entry.id);
        if (!current || (current.status !== 'queued' && current.status !== 'failed')) {
          continue;
        }
        this.patch(entry.id, (found) => ({ ...found, status: 'sending', attempts: found.attempts + 1 }));

        try {
          const sale = await firstValueFrom(sender(current.input));
          this.patch(entry.id, (found) => ({
            ...found,
            status: 'confirmed',
            failure: undefined,
            saleId: sale.id,
            invoiceNumber: sale.invoiceNumber,
          }));
        } catch (error) {
          const failure = OfflineQueueService.toFailure(error);
          this.patch(entry.id, (found) => ({ ...found, status: 'failed', failure }));
          if (failure.status === 0) {
            break;
          }
        }
      }
    } finally {
      this.syncing.set(false);
    }
  }

  private patch(id: string, apply: (entry: QueuedCheckout) => QueuedCheckout): void {
    this.update(this.entries().map((entry) => (entry.id === id ? apply(entry) : entry)));
  }

  private update(next: readonly QueuedCheckout[]): void {
    this.entries.set(next);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
    } catch {
      // Quota/private-mode: keep the in-memory queue; sync still works this session.
    }
  }

  private static readStored(): readonly QueuedCheckout[] {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) {
        return [];
      }
      const parsed = JSON.parse(raw) as QueuedCheckout[];
      if (!Array.isArray(parsed)) {
        return [];
      }
      // Anything caught mid-send by a reload goes back to queued — the idempotency key
      // makes the resend safe.
      return parsed.map((entry) =>
        entry.status === 'sending' ? { ...entry, status: 'queued' } : entry,
      );
    } catch {
      return [];
    }
  }

  private static toFailure(error: unknown): ApiFailure {
    if (error instanceof HttpErrorResponse) {
      const body = (error.error ?? {}) as { code?: string; detail?: string };
      return {
        status: error.status,
        code: typeof body.code === 'string' ? body.code : undefined,
        detail: typeof body.detail === 'string' ? body.detail : undefined,
      };
    }
    return { status: 0 };
  }
}
