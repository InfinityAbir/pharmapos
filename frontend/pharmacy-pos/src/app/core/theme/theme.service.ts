import { Injectable, signal } from '@angular/core';

export type AppTheme = 'light' | 'dark';

const STORAGE_KEY = 'pharmapos.theme';

/**
 * Color theme: light default, dark on request, persisted across sessions.
 * Applied as `data-theme` on <html> so the token layer in styles.css flips;
 * component state is never touched by a theme change.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly current = signal<AppTheme>(readStoredTheme());

  readonly theme = this.current.asReadonly();

  constructor() {
    applyTheme(this.current());
  }

  toggle(): void {
    this.setTheme(this.current() === 'light' ? 'dark' : 'light');
  }

  setTheme(theme: AppTheme): void {
    this.current.set(theme);
    try {
      localStorage.setItem(STORAGE_KEY, theme);
    } catch {
      // Private-mode storage failures must never break the UI.
    }
    applyTheme(theme);
  }
}

function readStoredTheme(): AppTheme {
  try {
    if (localStorage.getItem(STORAGE_KEY) === 'dark') {
      return 'dark';
    }
    if (
      localStorage.getItem(STORAGE_KEY) === null &&
      window.matchMedia?.('(prefers-color-scheme: dark)').matches
    ) {
      return 'dark';
    }
  } catch {
    // Storage or matchMedia unavailable (SSR/test): fall back to light.
  }
  return 'light';
}

function applyTheme(theme: AppTheme): void {
  try {
    document.documentElement.dataset['theme'] = theme;
  } catch {
    // Non-DOM test environments: nothing to apply.
  }
}
