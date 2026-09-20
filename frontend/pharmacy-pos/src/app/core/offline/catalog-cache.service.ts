import { Injectable, signal } from '@angular/core';
import type { Medicine } from '../http/catalog-api.service';

interface CachedMedicine {
  readonly id: string;
  readonly brandName: string;
  readonly genericName: string;
  readonly strength: string;
  readonly sellingPrice: number;
  readonly cachedAt: string;
}

const STORAGE_KEY = 'pharmapos.catalogCache.v1';
const MAX_CACHED = 200;

/**
 * Display-only catalog fallback for offline POS searching. Cached entries carry identity
 * and last-seen estimates — never stock levels — because pricing and availability are
 * always re-resolved server-side at sync. Results are labeled cached in the UI.
 */
@Injectable({ providedIn: 'root' })
export class CatalogCacheService {
  private readonly cache = signal<readonly CachedMedicine[]>(CatalogCacheService.readStored());

  store(medicines: readonly Medicine[]): void {
    const stampedAt = new Date().toISOString();
    const byId = new Map(this.cache().map((entry) => [entry.id, entry]));
    for (const medicine of medicines) {
      byId.set(medicine.id, {
        id: medicine.id,
        brandName: medicine.brandName,
        genericName: medicine.genericName,
        strength: medicine.strength,
        sellingPrice: medicine.sellingPrice,
        cachedAt: stampedAt,
      });
    }
    const next = [...byId.values()].slice(-MAX_CACHED);
    this.cache.set(next);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
    } catch {
      // Quota/private-mode: memory cache still serves this session.
    }
  }

  search(term: string, take = 20): readonly CachedMedicine[] {
    const needle = term.trim().toLowerCase();
    if (!needle) {
      return [];
    }
    return this.cache()
      .filter(
        (entry) =>
          entry.brandName.toLowerCase().includes(needle) ||
          entry.genericName.toLowerCase().includes(needle),
      )
      .slice(0, take);
  }

  get size(): number {
    return this.cache().length;
  }

  private static readStored(): readonly CachedMedicine[] {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) {
        return [];
      }
      const parsed = JSON.parse(raw) as CachedMedicine[];
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  }
}
