import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { describeApiError } from '../../core/i18n/error-message';
import { TranslatePipe } from '../../core/i18n/i18n.pipes';
import { LanguageService } from '../../core/i18n/language.service';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Member, Role, TenancyApi } from '../../core/http/tenancy-api.service';

@Component({
  selector: 'app-team',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './team.html',
  styleUrl: './team.css',
})
export class TeamPage implements OnInit {
  private readonly api = inject(TenancyApi);
  protected readonly lang = inject(LanguageService);

  protected readonly members = signal<readonly Member[]>([]);
  protected readonly roles = signal<readonly Role[]>([]);
  protected readonly loading = signal(true);
  protected readonly forbidden = signal(false);
  protected readonly rolesUnavailable = signal(false);
  protected readonly error = signal<string | undefined>(undefined);
  protected readonly savingId = signal<string | undefined>(undefined);

  /** Draft role-name selections per membership, keyed by membership id. */
  protected drafts = new Map<string, string[]>();

  ngOnInit(): void {
    void this.load();
  }

  protected draftFor(member: Member): string[] {
    let draft = this.drafts.get(member.membershipId);
    if (!draft) {
      draft = [...member.roles];
      this.drafts.set(member.membershipId, draft);
    }
    return draft;
  }

  protected toggleRole(member: Member, roleName: string, checked: boolean): void {
    const draft = this.draftFor(member);
    const next = checked
      ? [...draft, roleName]
      : draft.filter((entry) => entry !== roleName);
    this.drafts.set(member.membershipId, next);
  }

  protected draftChanged(member: Member): boolean {
    const draft = this.drafts.get(member.membershipId) ?? [...member.roles];
    return (
      draft.length !== member.roles.length ||
      draft.some((entry) => !member.roles.includes(entry))
    );
  }

  protected async saveRoles(member: Member): Promise<void> {
    const draft = this.drafts.get(member.membershipId) ?? [...member.roles];
    const ids = this.roles()
      .filter((role) => draft.includes(role.name))
      .map((role) => role.id);

    this.savingId.set(member.membershipId);
    this.error.set(undefined);
    try {
      const updated = await firstValueFrom(this.api.replaceMembershipRoles(member.membershipId, ids));
      this.drafts.set(member.membershipId, [...updated.roles]);
      await this.reloadMembers();
    } catch (error) {
      this.error.set(this.describe(error, 'team.rolesFailed'));
    } finally {
      this.savingId.set(undefined);
    }
  }

  protected async setStatus(member: Member, status: 'Active' | 'Inactive'): Promise<void> {
    this.error.set(undefined);
    try {
      await firstValueFrom(this.api.setMembershipStatus(member.membershipId, status));
      await this.reloadMembers();
    } catch (error) {
      this.error.set(this.describe(error, 'team.statusFailed'));
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(undefined);
    this.forbidden.set(false);
    this.rolesUnavailable.set(false);

    try {
      this.members.set(await firstValueFrom(this.api.listMembers()));
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        this.forbidden.set(true);
      } else {
        this.error.set(this.lang.t('team.loadFailed'));
      }
    }

    if (!this.forbidden()) {
      try {
        this.roles.set(await firstValueFrom(this.api.listRoles()));
      } catch (error) {
        // Role assignment needs tenant.roles.manage; listing members does not. The page stays
        // usable for status changes when the caller may list members but not roles.
        this.rolesUnavailable.set(true);
      }
    }

    this.loading.set(false);
  }

  private async reloadMembers(): Promise<void> {
    try {
      this.members.set(await firstValueFrom(this.api.listMembers()));
    } catch (error) {
        this.error.set(this.lang.t('team.reloadFailed'));
    }
  }

  private describe(error: unknown, fallbackKey: string): string {
    if (error instanceof HttpErrorResponse) {
      const body = (error.error ?? {}) as { code?: string };
      if (!body.code && error.status === 404) {
        return this.lang.t('team.referenceGone');
      }
    }
    return describeApiError(this.lang, error, fallbackKey);
  }
}
