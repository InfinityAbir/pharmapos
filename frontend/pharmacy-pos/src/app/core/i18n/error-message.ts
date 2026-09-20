import { HttpErrorResponse } from '@angular/common/http';
import { LanguageService } from './language.service';

/** Shape of the RFC 7807 problem bodies returned by the API. */
interface ProblemBody {
  readonly detail?: string;
  readonly code?: string;
}

/**
 * Maps a failed API call to a localized message. The backend already returns stable
 * `extensions.code` values (e.g. `sale.insufficient_stock`); each maps to an
 * `errors.*` translation key. Fallbacks: server English detail, then a generic message.
 * Domain identifiers (methods, types, invoice numbers) are never translated.
 */
export function describeApiError(
  lang: LanguageService,
  error: unknown,
  fallbackKey: string,
): string {
  if (error instanceof HttpErrorResponse) {
    const body = (error.error ?? {}) as ProblemBody;
    if (typeof body.code === 'string' && body.code.length > 0) {
      const key = `errors.${body.code.replaceAll('.', '_')}`;
      const translated = lang.t(key);
      if (translated !== key) {
        return translated;
      }
    }
    if (typeof body.detail === 'string' && body.detail.length > 0) {
      return body.detail;
    }
    if (error.status === 403) {
      return lang.t('errors.forbidden');
    }
    if (error.status === 0) {
      return lang.t('errors.offline');
    }
  }
  return lang.t(fallbackKey);
}
