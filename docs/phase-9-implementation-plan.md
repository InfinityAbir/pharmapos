# Phase 9 — Offline + Hardening: implementation plan

> Scope per the PRD roadmap ("Phase 9 - Hardening": security review, concurrency tests,
> integration tests, E2E tests, performance testing, error handling, audit review) and
> the offline brief (offline-safe POS, queue + sync, idempotency, conflict handling).
> Guiding rule: safe and reliable over complex; no fake functionality, no redesign.

## Offline (frontend only — the backend already returns idempotent, atomic operations)

- `ConnectivityService`: `navigator.onLine` + online/offline events plus a timed
  heartbeat against anonymous `GET /platform/info` (5s). The UI treats the API as
  reachable only after a successful heartbeat.
- `OfflineQueueService`: durable FIFO queue for **checkout intents only**, persisted in
  `localStorage` (`pharmapos.offlineQueue.v1`, capped at 50, in-memory fallback on
  quota/private-mode failure). Entries carry the cart's original idempotency key plus
  the tenant id. States: queued → sending → confirmed/failed. Reloads reset
  mid-send entries to queued (resend is safe by key).
- Sync (`syncAll`, per tenant, oldest first): success — fresh or idempotent replay,
  the server treats both identically — confirms the entry with invoice number. API
  failures park the entry with the server code/detail for manual resolution (never
  auto-dropped, never faked). Transport failure (status 0) stops the sweep; business
  rejections continue with the rest. Tenant-scoped: other-tenant entries are never
  sent and are flagged in the UI.
- `CatalogCacheService`: display-only fallback from recently seen medicines (identity
  + last-seen estimates, no stock levels), labeled cached; pricing/availability always
  re-resolve server-side at sync.
- POS integration: offline banner, complete → queue + cart cleared for the next
  customer, queued list with per-entry status/reason/retry/dismiss, confirmed receipt
  viewing, sync-now, auto-sync on reconnect via effect (no-op when empty, never touches
  the active cart). Returns stay online-only with an honest notice (they need live
  stock truth). 17 new `offline.*` keys in both languages.

## Hardening (verification pass + one fix)

- Already solid, verified not rebuilt: JWT validation, permission auth, CORS
  allow-list, per-user/IP rate limiting, security headers, HSTS/HTTPS, RFC 7807 errors
  without internals, global MediatR validation, tenant isolation tests, no secrets /
  `innerHTML` / token storage in the SPA.
- Query audit (all `ToList` sites): lists paged, aggregates server-side; one wart
  fixed — purchase monthly summary now `COUNT`/`SUM`s instead of loading the month's
  rows (same contract, mirrors the Phase 7 today-summary fix).
- Not changed (deployment-owned or unneeded): `AllowedHosts`, Kestrel request limits,
  caching/materialized views (measure first per the Phase 7 principle).

## Risks accepted and documented

- Offline sales skip live stock checks → oversell possible at sync; FIFO replay +
  server FEFO + visible 422 conflicts mitigate, manual resolution required.
- Token expiry offline → queue waits for a successful refresh on reconnect.
- Multi-tab double sync → safe via idempotency dedupe. Storage failure → session-only
  queue with no silent loss (entries stay visible until confirmed).

## Definition of Done

- Frontend build clean; specs green (connectivity, queue replay/conflict/isolation,
  catalog cache, i18n parity); backend build + unit green (untouched logic + one
  aggregate fix); docs updated, phase committed and pushed.
- Live-stack E2E (online sale, loss mid-workflow, queue + resync, duplicate retry,
  changed-stock conflict, concurrency, isolation, Phase 0–8 regression) and
  Docker-backed integration remain manual steps where Docker/Keycloak run.
