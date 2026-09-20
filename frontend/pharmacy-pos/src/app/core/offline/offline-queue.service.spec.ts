// @vitest-environment jsdom
import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { beforeAll, beforeEach, describe, expect, it } from 'vitest';
import { of, throwError } from 'rxjs';
import type { CheckoutInput, Sale } from '../http/sales-api.service';
import { OfflineQueueService } from './offline-queue.service';

function testInput(key: string): CheckoutInput {
  return {
    items: [],
    payments: [],
    discountAmount: 0,
    customerId: null,
    customerName: null,
    customerPhone: null,
    prescriptionReference: null,
    idempotencyKey: key,
  };
}

function testSale(): Sale {
  return {
    id: 'sale-1',
    invoiceNumber: 'INV-000001',
    occurredAtUtc: new Date().toISOString(),
    subtotal: 10,
    discountAmount: 0,
    totalAmount: 10,
    paidAmount: 10,
    changeDue: 0,
    customerId: null,
    customerName: null,
    customerPhone: null,
    prescriptionReference: null,
    cashierSubject: null,
    items: [],
    payments: [],
  };
}

beforeAll(() => {
  TestBed.configureTestingModule({});
});

describe('OfflineQueueService', () => {
  let queue: OfflineQueueService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [OfflineQueueService] });
    queue = TestBed.inject(OfflineQueueService);
  });

  it('persists enqueued intents across service instances', () => {
    queue.enqueue('tenant-a', testInput('key-1'));

    const reloaded = TestBed.inject(OfflineQueueService);
    void reloaded;
    expect(queue.pendingCount()).toBe(1);
    expect(JSON.parse(localStorage.getItem('pharmapos.offlineQueue.v1') as string)).toHaveLength(1);
  });

  it('confirms entries on successful sync, replay or fresh alike', async () => {
    const entry = queue.enqueue('tenant-a', testInput('key-1'));

    await queue.syncAll('tenant-a', () => of(testSale()));

    const done = queue.items().find((item) => item.id === entry.id);
    expect(done?.status).toBe('confirmed');
    expect(done?.invoiceNumber).toBe('INV-000001');
    expect(queue.pendingCount()).toBe(0);
  });

  it('parks business rejections with the server reason and continues the rest', async () => {
    queue.enqueue('tenant-a', testInput('key-1'));
    queue.enqueue('tenant-a', testInput('key-2'));

    let calls = 0;
    await queue.syncAll('tenant-a', (input) => {
      calls += 1;
      return input.idempotencyKey === 'key-1'
        ? throwError(
            () =>
              new HttpErrorResponse({
                status: 422,
                error: { code: 'sale.insufficient_stock', detail: 'Short.' },
              }),
          )
        : of(testSale());
    });

    expect(calls).toBe(2);
    const statuses = queue.items().map((item) => item.status);
    expect(statuses).toContain('failed');
    expect(statuses).toContain('confirmed');
    const failed = queue.items().find((item) => item.status === 'failed');
    expect(failed?.failure).toMatchObject({ status: 422, code: 'sale.insufficient_stock' });
  });

  it('stops at transport failures and resumes on retry', async () => {
    queue.enqueue('tenant-a', testInput('key-1'));
    queue.enqueue('tenant-a', testInput('key-2'));

    await queue.syncAll('tenant-a', () =>
      throwError(() => new HttpErrorResponse({ status: 0, error: {} })),
    );

    expect(queue.items().filter((item) => item.status === 'failed')).toHaveLength(1);
    expect(queue.items().filter((item) => item.status === 'queued')).toHaveLength(1);

    queue.items()
      .filter((item) => item.status === 'failed')
      .forEach((item) => queue.markRetrying(item.id));
    await queue.syncAll('tenant-a', () => of(testSale()));

    expect(queue.pendingCount()).toBe(0);
  });

  it('never syncs another tenant’s entries', async () => {
    queue.enqueue('tenant-a', testInput('key-1'));

    await queue.syncAll('tenant-b', () => of(testSale()));

    expect(queue.pendingCount()).toBe(1);
    expect(queue.pendingForTenant('tenant-b')).toHaveLength(0);
  });

  it('resets entries caught mid-send by a reload back to queued', () => {
    localStorage.setItem(
      'pharmapos.offlineQueue.v1',
      JSON.stringify([
        {
          id: 'q1',
          tenantId: 'tenant-a',
          input: testInput('key-1'),
          createdAt: new Date().toISOString(),
          status: 'sending',
          attempts: 1,
        },
      ]),
    );

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [OfflineQueueService] });
    const reloaded = TestBed.inject(OfflineQueueService);

    expect(reloaded.items()[0].status).toBe('queued');
  });
});
