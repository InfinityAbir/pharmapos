// @vitest-environment jsdom
import { beforeEach, describe, expect, it } from 'vitest';
import type { Medicine } from '../http/catalog-api.service';
import { CatalogCacheService } from './catalog-cache.service';

function medicine(id: string, brand: string, generic: string): Medicine {
  return {
    id,
    brandName: brand,
    genericName: generic,
    strength: '500mg',
    dosageForm: 'Tablet',
    manufacturerId: null,
    manufacturerName: null,
    categoryId: null,
    categoryName: null,
    barcode: null,
    unit: 'strip',
    packSize: 10,
    purchasePrice: 8,
    sellingPrice: 10,
    reorderLevel: 10,
    vatRate: 0,
    requiresPrescription: false,
    isActive: true,
  } as Medicine;
}

describe('CatalogCacheService', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('finds cached medicines by brand or generic, case-insensitively', () => {
    const cache = new CatalogCacheService();
    cache.store([medicine('m1', 'Napa', 'Paracetamol'), medicine('m2', 'Seclo', 'Omeprazole')]);

    expect(cache.search('nap').map((entry) => entry.id)).toEqual(['m1']);
    expect(cache.search('OMEPRAZOLE').map((entry) => entry.id)).toEqual(['m2']);
    expect(cache.search('xyz')).toEqual([]);
  });

  it('refreshes entries on re-store and survives reload', () => {
    const cache = new CatalogCacheService();
    cache.store([medicine('m1', 'Napa', 'Paracetamol')]);

    const reloaded = new CatalogCacheService();
    expect(reloaded.size).toBe(1);
    expect(reloaded.search('napa')[0].sellingPrice).toBe(10);
  });
});
