import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { PERMISSION_CATALOG } from '../../core/auth/permissions';
import { Role, TenancyApi } from '../../core/http/tenancy-api.service';
import { describeApiError } from '../../core/i18n/error-message';
import { TranslatePipe } from '../../core/i18n/i18n.pipes';
import { LanguageService } from '../../core/i18n/language.service';

@Component({
  selector: 'app-roles',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './roles.html',
  styleUrl: './roles.css',
})
export class RolesPage implements OnInit {
  private readonly api = inject(TenancyApi);
  protected readonly lang = inject(LanguageService);

  protected readonly catalog = PERMISSION_CATALOG;
  protected readonly roles = signal<readonly Role[]>([]);
  protected readonly loading = signal(true);
  protected readonly forbidden = signal(false);
  protected readonly error = signal<string | undefined>(undefined);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | undefined>(undefined);
  protected readonly editingId = signal<string | undefined>(undefined);

  protected name = '';
  protected description = '';
  protected selected: string[] = [];

  ngOnInit(): void {
    void this.load();
  }

  protected togglePermission(code: string, checked: boolean): void {
    this.selected = checked
      ? [...this.selected, code]
      : this.selected.filter((entry) => entry !== code);
  }

  protected startCreate(): void {
    this.editingId.set(undefined);
    this.name = '';
    this.description = '';
    this.selected = [];
    this.formError.set(undefined);
  }

  protected startEdit(role: Role): void {
    this.editingId.set(role.id);
    this.name = role.name;
    this.description = role.description ?? '';
    this.selected = [...role.permissions];
    this.formError.set(undefined);
  }

  protected async save(): Promise<void> {
    this.formError.set(undefined);

    const name = this.name.trim();
    if (!name) {
      this.formError.set(this.lang.t('roles.nameRequired'));
      return;
    }

    const description = this.description.trim() || null;
    const permissions = [...this.selected];
    const editingId = this.editingId();

    this.saving.set(true);
    try {
      if (editingId) {
        await firstValueFrom(this.api.updateRole(editingId, name, description, permissions));
      } else {
        await firstValueFrom(this.api.createRole(name, description, permissions));
      }
      this.startCreate();
      await this.load();
    } catch (error) {
      this.formError.set(this.describe(error, 'roles.saveFailed'));
    } finally {
      this.saving.set(false);
    }
  }

  protected async remove(role: Role): Promise<void> {
    if (role.isSystem) {
      return;
    }
    this.error.set(undefined);
    try {
      await firstValueFrom(this.api.deleteRole(role.id));
      await this.load();
    } catch (error) {
      this.error.set(this.describe(error, 'roles.deleteFailed'));
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(undefined);
    this.forbidden.set(false);

    try {
      this.roles.set(await firstValueFrom(this.api.listRoles()));
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.forbidden.set(true);
      } else {
        this.error.set(this.lang.t('roles.loadFailed'));
      }
    } finally {
      this.loading.set(false);
    }
  }

  private describe(error: unknown, fallbackKey: string): string {
    if (error instanceof HttpErrorResponse) {
      const body = (error.error ?? {}) as { code?: string };
      if (!body.code) {
        if (error.status === 409) {
          return this.lang.t('roles.nameConflict');
        }
        if (error.status === 403) {
          return this.lang.t('roles.systemLocked');
        }
      }
    }
    return describeApiError(this.lang, error, fallbackKey);
  }
}
