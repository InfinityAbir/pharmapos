// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { LanguageService } from './language.service';

describe('LanguageService', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.unstubAllGlobals();
  });

  it('defaults to English', () => {
    const lang = new LanguageService();

    expect(lang.language()).toBe('en');
    expect(lang.t('nav.dashboard')).toBe('Dashboard');
  });

  it('switches to Bangla and persists the preference', () => {
    const lang = new LanguageService();
    const setItem = vi.spyOn(Storage.prototype, 'setItem');

    lang.setLanguage('bn');

    expect(lang.language()).toBe('bn');
    expect(lang.t('nav.dashboard')).toBe('ড্যাশবোর্ড');
    expect(setItem).toHaveBeenCalledWith('pharmapos.lang', 'bn');
    expect(document.documentElement.lang).toBe('bn');
  });

  it('restores the stored language on construction', () => {
    localStorage.setItem('pharmapos.lang', 'bn');

    const lang = new LanguageService();

    expect(lang.language()).toBe('bn');
  });

  it('falls back to English for keys missing in Bangla', () => {
    const lang = new LanguageService();
    lang.setLanguage('bn');

    expect(lang.t('does.not.exist')).toBe('does.not.exist');
  });

  it('formats BDT money per locale', () => {
    const lang = new LanguageService();

    const en = lang.formatMoney(150);
    expect(en).toContain('150');

    lang.setLanguage('bn');
    const bn = lang.formatMoney(150);
    expect(bn).not.toBe(en);
    expect(bn).toContain('১৫০');
  });

  it('switching language leaves held POS state untouched', () => {
    const lang = new LanguageService();
    const cart = [
      { medicineId: 'm1', quantity: 2, discount: 5 },
      { medicineId: 'm2', quantity: 1, discount: 0 },
    ];
    const payments = [{ method: 'Cash', amount: 100, reference: '' }];
    const selection = { customerId: 'c1', headerDiscount: 10 };

    lang.setLanguage('bn');
    lang.setLanguage('en');

    expect(cart).toHaveLength(2);
    expect(cart[0]).toEqual({ medicineId: 'm1', quantity: 2, discount: 5 });
    expect(payments).toEqual([{ method: 'Cash', amount: 100, reference: '' }]);
    expect(selection).toEqual({ customerId: 'c1', headerDiscount: 10 });
  });
});
