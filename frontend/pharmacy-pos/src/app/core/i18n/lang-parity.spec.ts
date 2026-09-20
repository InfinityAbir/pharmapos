import { describe, expect, it } from 'vitest';
import { LANG_BN } from './lang-bn';
import { LANG_EN } from './lang-en';

describe('translation dictionaries', () => {
  it('contain the same keys in English and Bangla (fallback covers gaps, parity is the rule)', () => {
    const en = Object.keys(LANG_EN).sort();
    const bn = Object.keys(LANG_BN).sort();

    expect(bn).toEqual(en);
  });

  it('have no empty translations', () => {
    for (const [key, value] of Object.entries(LANG_BN)) {
      expect(value.trim().length, key).toBeGreaterThan(0);
    }
  });
});
