import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { CatalogApi, Category, Manufacturer } from '../../core/http/catalog-api.service';

@Component({
  selector: 'app-catalog-data',
  imports: [FormsModule],
  templateUrl: './catalog-data.html',
  styleUrl: './catalog-data.css',
})
export class CatalogDataPage implements OnInit {
  private readonly api = inject(CatalogApi);

  protected readonly categories = signal<readonly Category[]>([]);
  protected readonly manufacturers = signal<readonly Manufacturer[]>([]);
  protected readonly loading = signal(true);
  protected readonly forbidden = signal(false);
  protected readonly error = signal<string | undefined>(undefined);
  protected readonly notice = signal<string | undefined>(undefined);

  protected categoryName = '';
  protected categoryDescription = '';
  protected editingCategoryId: string | undefined;

  protected manufacturerName = '';
  protected manufacturerCountry = '';
  protected editingManufacturerId: string | undefined;

  ngOnInit(): void {
    void this.load();
  }

  protected startEditCategory(category: Category): void {
    this.editingCategoryId = category.id;
    this.categoryName = category.name;
    this.categoryDescription = category.description ?? '';
    this.notice.set(undefined);
  }

  protected cancelEditCategory(): void {
    this.editingCategoryId = undefined;
    this.categoryName = '';
    this.categoryDescription = '';
  }

  protected async saveCategory(): Promise<void> {
    const name = this.categoryName.trim();
    if (!name) {
      this.notice.set('A category name is required.');
      return;
    }

    this.notice.set(undefined);
    this.error.set(undefined);
    try {
      if (this.editingCategoryId) {
        await firstValueFrom(
          this.api.updateCategory(this.editingCategoryId, name, this.empty(this.categoryDescription)),
        );
      } else {
        await firstValueFrom(
          this.api.createCategory(name, this.empty(this.categoryDescription)),
        );
      }
      this.cancelEditCategory();
      await this.reload();
    } catch (error) {
      this.notice.set(this.describe(error, 'The category could not be saved.', 'category'));
    }
  }

  protected async removeCategory(category: Category): Promise<void> {
    this.notice.set(undefined);
    this.error.set(undefined);
    try {
      await firstValueFrom(this.api.deleteCategory(category.id));
      await this.reload();
    } catch (error) {
      this.notice.set(this.describe(error, 'The category could not be deleted.', 'category'));
    }
  }

  protected startEditManufacturer(manufacturer: Manufacturer): void {
    this.editingManufacturerId = manufacturer.id;
    this.manufacturerName = manufacturer.name;
    this.manufacturerCountry = manufacturer.country ?? '';
    this.notice.set(undefined);
  }

  protected cancelEditManufacturer(): void {
    this.editingManufacturerId = undefined;
    this.manufacturerName = '';
    this.manufacturerCountry = '';
  }

  protected async saveManufacturer(): Promise<void> {
    const name = this.manufacturerName.trim();
    if (!name) {
      this.notice.set('A manufacturer name is required.');
      return;
    }

    this.notice.set(undefined);
    this.error.set(undefined);
    try {
      if (this.editingManufacturerId) {
        await firstValueFrom(
          this.api.updateManufacturer(
            this.editingManufacturerId,
            name,
            this.empty(this.manufacturerCountry),
          ),
        );
      } else {
        await firstValueFrom(
          this.api.createManufacturer(name, this.empty(this.manufacturerCountry)),
        );
      }
      this.cancelEditManufacturer();
      await this.reload();
    } catch (error) {
      this.notice.set(this.describe(error, 'The manufacturer could not be saved.', 'manufacturer'));
    }
  }

  protected async removeManufacturer(manufacturer: Manufacturer): Promise<void> {
    this.notice.set(undefined);
    this.error.set(undefined);
    try {
      await firstValueFrom(this.api.deleteManufacturer(manufacturer.id));
      await this.reload();
    } catch (error) {
      this.notice.set(this.describe(error, 'The manufacturer could not be deleted.', 'manufacturer'));
    }
  }

  private empty(value: string): string | null {
    return value.trim() === '' ? null : value.trim();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(undefined);
    this.forbidden.set(false);

    try {
      await this.reload();
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.forbidden.set(true);
      } else {
        this.error.set('The catalog data could not be loaded.');
      }
    } finally {
      this.loading.set(false);
    }
  }

  private async reload(): Promise<void> {
    const [categories, manufacturers] = await Promise.all([
      firstValueFrom(this.api.listCategories()),
      firstValueFrom(this.api.listManufacturers()),
    ]);
    this.categories.set(categories);
    this.manufacturers.set(manufacturers);
  }

  private describe(error: unknown, fallback: string, kind: 'category' | 'manufacturer'): string {
    if (error instanceof HttpErrorResponse) {
      const detail = (error.error as { detail?: string } | undefined)?.detail;
      if (typeof detail === 'string' && detail.length > 0) {
        return detail;
      }
      if (error.status === 409) {
        return error.url?.includes('manufacturers') || kind === 'manufacturer'
          ? 'This manufacturer cannot be deleted while medicines reference it, and names must be unique.'
          : 'This category cannot be deleted while medicines reference it, and names must be unique.';
      }
    }
    return fallback;
  }
}
