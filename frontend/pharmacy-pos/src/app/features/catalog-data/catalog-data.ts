import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { CatalogApi, Category, Manufacturer } from '../../core/http/catalog-api.service';
import { describeApiError } from '../../core/i18n/error-message';
import { TranslatePipe } from '../../core/i18n/i18n.pipes';
import { LanguageService } from '../../core/i18n/language.service';

@Component({
  selector: 'app-catalog-data',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './catalog-data.html',
  styleUrl: './catalog-data.css',
})
export class CatalogDataPage implements OnInit {
  private readonly api = inject(CatalogApi);
  protected readonly lang = inject(LanguageService);

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
      this.notice.set(this.lang.t('catalog.categoryRequired'));
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
      this.notice.set(this.describe(error, 'catalog.saveFailed', 'category'));
    }
  }

  protected async removeCategory(category: Category): Promise<void> {
    this.notice.set(undefined);
    this.error.set(undefined);
    try {
      await firstValueFrom(this.api.deleteCategory(category.id));
      await this.reload();
    } catch (error) {
      this.notice.set(this.describe(error, 'catalog.deleteFailed', 'category'));
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
      this.notice.set(this.lang.t('catalog.manufacturerRequired'));
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
      this.notice.set(this.describe(error, 'catalog.saveFailed', 'manufacturer'));
    }
  }

  protected async removeManufacturer(manufacturer: Manufacturer): Promise<void> {
    this.notice.set(undefined);
    this.error.set(undefined);
    try {
      await firstValueFrom(this.api.deleteManufacturer(manufacturer.id));
      await this.reload();
    } catch (error) {
      this.notice.set(this.describe(error, 'catalog.deleteFailed', 'manufacturer'));
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
        this.error.set(this.lang.t('catalog.loadFailed'));
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

  private describe(error: unknown, fallbackKey: string, kind: 'category' | 'manufacturer'): string {
    if (error instanceof HttpErrorResponse) {
      const body = (error.error ?? {}) as { code?: string };
      if (!body.code && error.status === 409) {
        return this.lang.t(
          kind === 'manufacturer' ? 'catalog.manufacturerConflict' : 'catalog.categoryConflict',
        );
      }
    }
    return describeApiError(this.lang, error, fallbackKey);
  }
}
