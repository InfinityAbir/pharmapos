// @vitest-environment jsdom
import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { beforeAll, beforeEach, describe, expect, it } from 'vitest';
import { describeApiError } from './error-message';
import { LanguageService } from './language.service';

beforeAll(() => {
  TestBed.configureTestingModule({});
});

beforeEach(() => {
  localStorage.clear();
});

function failure(code: string | undefined, detail: string, status = 422): HttpErrorResponse {
  return new HttpErrorResponse({
    status,
    error: { detail, ...(code ? { code } : {}) },
  });
}

describe('describeApiError', () => {
  it('maps backend error codes to localized messages', () => {
    const lang = new LanguageService();

    const message = describeApiError(
      lang,
      failure('sale.insufficient_stock', 'Not enough sellable stock...'),
      'errors.generic',
    );

    expect(message).toBe('Not enough sellable stock for the requested quantity.');
  });

  it('localizes mapped codes in Bangla', () => {
    const lang = new LanguageService();
    lang.setLanguage('bn');

    const message = describeApiError(
      lang,
      failure('sale.underpaid', 'Paid 5 is less than the total 10.'),
      'errors.generic',
    );

    expect(message).toBe('পরিশোধ মোট বিক্রয়ের কম।');
  });

  it('falls back to the server detail for unmapped codes', () => {
    const lang = new LanguageService();

    const message = describeApiError(
      lang,
      failure('tenant.something_new', 'A brand-new server message.'),
      'errors.generic',
    );

    expect(message).toBe('A brand-new server message.');
  });

  it('falls back to the generic key for empty bodies', () => {
    const lang = new LanguageService();

    const message = describeApiError(
      lang,
      new HttpErrorResponse({ status: 500, error: {} }),
      'sales.historyFailed',
    );

    expect(message).toBe('Sales history could not be loaded.');
  });

  it('maps 403 without a code to the forbidden message', () => {
    const lang = new LanguageService();

    const message = describeApiError(
      lang,
      new HttpErrorResponse({ status: 403, error: {} }),
      'errors.generic',
    );

    expect(message).toBe('You do not have permission to perform this action.');
  });
});
