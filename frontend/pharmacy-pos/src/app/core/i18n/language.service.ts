import { Injectable, signal } from '@angular/core';
import { LANG_BN } from './lang-bn';
import { LANG_EN } from './lang-en';

export type AppLanguage = 'en' | 'bn';

const STORAGE_KEY = 'pharmapos.lang';

const LOCALES: Record<AppLanguage, string> = {
  en: 'en-BD',
  bn: 'bn-BD',
};

/**
 * Application language: English default, Bangla on request, persisted across sessions.
 * Switching flips only the locale signal — component state (carts, forms, selections)
 * is never touched, so an active POS sale survives a language change untouched.
 */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  protected readonly lang = signal<AppLanguage>(LanguageService.readStored());

  readonly language = this.lang.asReadonly();

  constructor() {
    LanguageService.applyDocumentLang(this.lang());
  }

  t(key: string, params?: Record<string, string | number>): string {
    const active = this.dictionary();
    const template = active[key] ?? LANG_EN[key] ?? key;
    if (!params) {
      return template;
    }
    return template.replace(/\{(\w+)\}/g, (match, name: string) =>
      params[name] === undefined ? match : String(params[name]),
    );
  }

  setLanguage(language: AppLanguage): void {
    this.lang.set(language);
    try {
      localStorage.setItem(STORAGE_KEY, language);
    } catch {
      // Private-mode storage failures must never break the UI.
    }
    LanguageService.applyDocumentLang(language);
  }

  toggle(): void {
    this.setLanguage(this.lang() === 'en' ? 'bn' : 'en');
  }

  locale(): string {
    return LOCALES[this.lang()];
  }

  formatMoney(amount: number): string {
    return new Intl.NumberFormat(this.locale(), {
      style: 'currency',
      currency: 'BDT',
    }).format(amount);
  }

  formatNumber(value: number): string {
    return new Intl.NumberFormat(this.locale()).format(value);
  }

  formatDate(value: string | Date): string {
    return new Intl.DateTimeFormat(this.locale(), { dateStyle: 'medium' }).format(
      new Date(value),
    );
  }

  formatDateTime(value: string | Date): string {
    return new Intl.DateTimeFormat(this.locale(), {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(value));
  }

  private dictionary(): Record<string, string> {
    return this.lang() === 'bn' ? LANG_BN : LANG_EN;
  }

  private static readStored(): AppLanguage {
    try {
      if (localStorage.getItem(STORAGE_KEY) === 'bn') {
        return 'bn';
      }
    } catch {
      // Storage unavailable (SSR/private mode): fall back to the default.
    }
    return 'en';
  }

  private static applyDocumentLang(language: AppLanguage): void {
    try {
      document.documentElement.lang = language === 'bn' ? 'bn' : 'en';
    } catch {
      // Non-DOM test environments: nothing to apply.
    }
  }
}
