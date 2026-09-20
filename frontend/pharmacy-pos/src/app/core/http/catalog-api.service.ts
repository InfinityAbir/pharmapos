import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AppConfigService } from '../config/app-config.service';

export interface Medicine {
  readonly id: string;
  readonly brandName: string;
  readonly genericName: string;
  readonly strength: string;
  readonly dosageForm: string;
  readonly manufacturerId: string | null;
  readonly manufacturerName: string | null;
  readonly categoryId: string | null;
  readonly categoryName: string | null;
  readonly barcode: string | null;
  readonly unit: string;
  readonly packSize: number | null;
  readonly purchasePrice: number;
  readonly sellingPrice: number;
  readonly reorderLevel: number;
  readonly vatRate: number | null;
  readonly requiresPrescription: boolean;
  readonly isActive: boolean;
}

export interface MedicineInput {
  brandName: string;
  genericName: string;
  strength: string;
  dosageForm: string;
  manufacturerId: string | null;
  categoryId: string | null;
  barcode: string | null;
  unit: string;
  packSize: number | null;
  purchasePrice: number;
  sellingPrice: number;
  reorderLevel: number;
  vatRate: number | null;
  requiresPrescription: boolean;
}

export interface MedicineSearch {
  search?: string;
  categoryId?: string;
  manufacturerId?: string;
  isActive?: boolean;
  skip?: number;
  take?: number;
}

export interface Category {
  readonly id: string;
  readonly name: string;
  readonly description: string | null;
}

export interface Manufacturer {
  readonly id: string;
  readonly name: string;
  readonly country: string | null;
}

export interface PharmacyProfile {
  pharmacyName: string;
  address: string | null;
  phone: string | null;
  email: string | null;
  website: string | null;
  registrationInfo: string | null;
  vatInfo: string | null;
  invoiceFooter: string | null;
  currency: string;
  defaultLanguage: string;
  timezone: string;
  logoUrl: string | null;
}

export interface CatalogSummary {
  readonly medicineCount: number;
  readonly activeMedicineCount: number;
  readonly categoryCount: number;
  readonly manufacturerCount: number;
}

/** Known dosage forms (mirrors the server list; `Other` is the escape hatch). */
export const DOSAGE_FORMS: readonly string[] = [
  'Tablet',
  'Capsule',
  'Syrup',
  'Suspension',
  'Injection',
  'Cream',
  'Ointment',
  'Drops',
  'Inhaler',
  'Powder',
  'Spray',
  'Suppository',
  'Other',
];

/**
 * Tenant catalog endpoints. Reads require `tenant.medicines.read`; writes require
 * `tenant.medicines.manage` (profile writes: `tenant.pharmacy.manage`). The API answers
 * 403 otherwise.
 */
@Injectable({ providedIn: 'root' })
export class CatalogApi {
  private readonly http = inject(HttpClient);
  private readonly appConfig = inject(AppConfigService);

  private get baseUrl(): string {
    return `${this.appConfig.apiBaseUrl}/tenant`;
  }

  catalogSummary(): Observable<CatalogSummary> {
    return this.http.get<CatalogSummary>(`${this.baseUrl}/catalog/summary`);
  }

  searchMedicines(filter: MedicineSearch = {}): Observable<readonly Medicine[]> {
    return this.http.get<readonly Medicine[]>(`${this.baseUrl}/medicines`, {
      params: {
        ...(filter.search ? { search: filter.search } : {}),
        ...(filter.categoryId ? { categoryId: filter.categoryId } : {}),
        ...(filter.manufacturerId ? { manufacturerId: filter.manufacturerId } : {}),
        ...(filter.isActive !== undefined ? { isActive: String(filter.isActive) } : {}),
        skip: String(filter.skip ?? 0),
        take: String(filter.take ?? 25),
      },
    });
  }

  getMedicine(id: string): Observable<Medicine> {
    return this.http.get<Medicine>(`${this.baseUrl}/medicines/${id}`);
  }

  getMedicineByBarcode(barcode: string): Observable<Medicine> {
    return this.http.get<Medicine>(
      `${this.baseUrl}/medicines/by-barcode/${encodeURIComponent(barcode)}`,
    );
  }

  createMedicine(input: MedicineInput): Observable<Medicine> {
    return this.http.post<Medicine>(`${this.baseUrl}/medicines`, input);
  }

  updateMedicine(id: string, input: MedicineInput): Observable<Medicine> {
    return this.http.put<Medicine>(`${this.baseUrl}/medicines/${id}`, input);
  }

  setMedicineStatus(id: string, isActive: boolean): Observable<Medicine> {
    return this.http.patch<Medicine>(`${this.baseUrl}/medicines/${id}/status`, { isActive });
  }

  listCategories(): Observable<readonly Category[]> {
    return this.http.get<readonly Category[]>(`${this.baseUrl}/categories`);
  }

  createCategory(name: string, description: string | null): Observable<Category> {
    return this.http.post<Category>(`${this.baseUrl}/categories`, { name, description });
  }

  updateCategory(id: string, name: string, description: string | null): Observable<Category> {
    return this.http.put<Category>(`${this.baseUrl}/categories/${id}`, { name, description });
  }

  deleteCategory(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/categories/${id}`);
  }

  listManufacturers(): Observable<readonly Manufacturer[]> {
    return this.http.get<readonly Manufacturer[]>(`${this.baseUrl}/manufacturers`);
  }

  createManufacturer(name: string, country: string | null): Observable<Manufacturer> {
    return this.http.post<Manufacturer>(`${this.baseUrl}/manufacturers`, { name, country });
  }

  updateManufacturer(id: string, name: string, country: string | null): Observable<Manufacturer> {
    return this.http.put<Manufacturer>(`${this.baseUrl}/manufacturers/${id}`, { name, country });
  }

  deleteManufacturer(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/manufacturers/${id}`);
  }

  getPharmacyProfile(): Observable<PharmacyProfile> {
    return this.http.get<PharmacyProfile>(`${this.baseUrl}/pharmacy-profile`);
  }

  upsertPharmacyProfile(profile: PharmacyProfile): Observable<PharmacyProfile> {
    return this.http.put<PharmacyProfile>(`${this.baseUrl}/pharmacy-profile`, profile);
  }
}
