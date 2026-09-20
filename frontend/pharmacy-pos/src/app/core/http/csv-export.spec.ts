import { describe, expect, it } from 'vitest';
import { toCsvRecords } from './csv-export';

describe('toCsvRecords', () => {
  it('renders headers and rows without computation', () => {
    const csv = toCsvRecords(['cashier', 'totalAmount'], [
      { cashier: 'aisha', totalAmount: 150 },
      { cashier: 'Unknown', totalAmount: 0 },
    ]);

    expect(csv).toBe('cashier,totalAmount\r\naisha,150\r\nUnknown,0\r\n');
  });

  it('escapes commas, quotes, and newlines', () => {
    const csv = toCsvRecords([{ key: 'name', header: 'Medicine' }], [
      { name: 'Napa "Extra", 500mg\nnew line' },
    ]);

    expect(csv).toBe('Medicine\r\n"Napa ""Extra"", 500mg\nnew line"\r\n');
  });

  it('renders nulls as empty cells', () => {
    const csv = toCsvRecords(['a', 'b'], [{ a: null, b: undefined }]);

    expect(csv).toBe('a,b\r\n,\r\n');
  });
});
