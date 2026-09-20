# Phase 8 — Bangla + English Localization: implementation plan

> Scope per the PRD roadmap ("Phase 8 - Localization": Bangla, English, localized
> receipts) and the phase brief (app-wide i18n, persisted switcher, locale-aware
> formatting, error-code-driven messages, POS cart preservation, no business-logic
> changes).

## Approach (runtime i18n, zero new dependencies)

- Hand-rolled `LanguageService` (signal, `localStorage` key `pharmapos.lang`, English
  default, `<html lang>` sync, Bangla-missing-key falls back to English) plus flat-key
  TS dictionaries (`lang-en.ts` / `lang-bn.ts`, 599 keys, parity enforced by spec).
  Bundled imports keep translations working offline; no fetched JSON, no loader states.
- `t` pipe (impure, supports `{param}` interpolation for differing word orders),
  `money` pipe (`Intl`, `en-BD`/`bn-BD`, BDT — Bengali digits in Bangla), `ldate` /
  `ldatetime` pipes replacing `DatePipe` and hardcoded `৳` interpolations.
- **Backend unchanged — by design.** `GlobalExceptionHandler` already returns stable
  `extensions.code` values; the shared `describeApiError` helper maps
  `code → errors.*` keys, then falls back to the server English `detail`, then a generic
  localized message. Validation 400s get a localized generic message.
- Identifiers stay language-neutral: payment methods, movement types, permission codes,
  invoice numbers, role/member names, and medicine names are never translated
  (permission *descriptions* are localized via `permdesc.*` keys; codes are untouched).

## Scope

- Switcher in the app header (always visible, works signed-out); preference persists.
- Localized: nav, header/footer, dashboard, POS, sales history, returns, receipts
  (labels, money, dates), customers, suppliers, purchases, inventory, medicines,
  catalog data, pharmacy profile, team, roles, tenants, audit, reports, all client-side
  validation/notice/error strings, and all mapped backend error codes.
- Bangla-capable system font-stack fallback in `styles.css`; Unicode throughout.

## POS safety

- The switch flips only the locale signal on the same route: no navigation, no
  component recreation, no cart/payment/customer/discount/total reset. Covered by a
  spec holding cart-like state across switches; the full
  English → cart → customer → discount → বাংলা → verify → complete (and reverse)
  flow, plus Bangla receipt/print verification, remains a manual E2E step on a live
  stack (it needs Keycloak + API + DB, unavailable in this session).

## Explicitly out of scope

- Backend message localization, server-side CSV/PDF language variants, RTL layout
  (Bangla is LTR), per-tenant default language wiring (the profile field exists;
  applying it to new sessions is future work), translating user-entered data.

## Definition of Done

- Frontend build clean; specs green (service, error-code mapping, CSV legacy,
  dictionary parity, POS-state preservation); backend build + unit green (untouched);
  docs updated, phase committed and pushed.
