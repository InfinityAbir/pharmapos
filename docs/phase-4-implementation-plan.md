# Phase 4 — Suppliers & Purchasing: implementation plan

> Scope per the PRD roadmap ("Phase 4 - Purchasing": suppliers, purchase, purchase items,
> receiving), PRD §12 (supplier fields/history), §13 (purchase workflow, atomic receiving),
> and architecture §4 (tenant-composite keys, `xmin`, `numeric(18,4)`, idempotency keys for
> purchase receiving, UTC).

## Motivation

Phase 3 proved manual stock movements. Phase 4 adds the business workflow that normally creates
stock: suppliers and purchases. A purchase is a reviewed document (draft → received/cancelled);
receiving posts batches and `PURCHASE_RECEIVED` movements **atomically** and is idempotent, so
retries and double-clicks can never double-post stock. Sale deduction (Phase 5) and returns
(Phase 6) reuse the same ledger and patterns.

## Scope

### Backend

- Domain (`PharmaPOS.Domain.Purchasing`, tenant-scoped, `IsMultiTenant()`):
  - `Supplier` — name/company/phone/email/address/contact/payment-terms/active,
    `UNIQUE (TenantId, Name)`.
  - `Purchase` — required tenant-composite FK to supplier, `PurchaseDate`, `Status`
    (`Draft`/`Received`/`Cancelled`), optional supplier invoice number, item + header discounts,
    tax, notes, server-computed `TotalAmount`; `xmin` token.
  - `PurchaseItem` — tenant-composite FKs to purchase and medicine, batch number,
    manufacture/expiry dates (expiry required), quantity, unit price, item discount, optional
    selling price (null = keep existing/catalog price); line totals computed, never trusted.
  - `IdempotencyKey` — tenant-scoped `UNIQUE (TenantId, Key)` → purchase mapping for receive.
- Workflow rules: create/update/cancel operate on drafts only; receive validates the whole
  document (medicines exist, expiries future, discounts sane) then writes batches + movements +
  status in **one** `SaveChanges`; repeat receive with the same key returns the stored purchase
  without re-posting; received/cancelled documents are immutable (422 otherwise).
- Batch upsert logic shared between manual receive and purchase receive via an internal
  `StockIntake` helper (same top-up/date-mismatch rules, no duplicated branching).
- Reads: supplier list (search) + detail, supplier purchase history, purchase list
  (supplier/status filter, paging), purchase detail with items.
- Permissions: `tenant.purchasing.read`, `tenant.purchasing.manage` (covers suppliers +
  purchases). Seed: Platform Admin + Owner ← both; Manager ← read; others unchanged.
- Audit: `supplier.created/updated/status.changed`, `purchase.created/updated/received/cancelled`.
- Migration `PurchasingInitialCreate` + idempotent seed: three demo suppliers and one received
  demo purchase (fixed idempotency key) adding a second Napa batch.

### Frontend

- `PurchasingApi`; nav entries **Suppliers** and **Purchases** (`purchasing.read`).
- Suppliers page (search + CRUD + status, history link); Purchases page (list with
  supplier/status filters, draft form with dynamic item rows + live server-computed preview on
  save, detail view with items, receive with client-generated idempotency key, cancel).
- Dashboard purchasing card (open drafts count, month received total)? — only if cheap via a
  summary endpoint; else deferred. (Decided during implementation: small summary endpoint.)

### Explicitly out of scope (deferred, documented)

- Supplier payments/balances and payment status (no payments module yet; recorded as deferred,
  not stubbed).
- Partial receiving (a receive posts the whole draft; split deliveries = separate drafts).
- Editing received/cancelled documents; purchase deletion (cancel covers it).
- Linking batches to the supplier entity (batches keep a denormalized supplier-name snapshot;
  supplier rename does not rewrite history).
- Automated reorder suggestions (alerts from Phase 3 remain the trigger).

## Decisions

- Draft workflow (not single-step) because the PRD workflow says review → receive, and the
  draft is where validation errors surface before stock moves.
- Totals always computed server-side; the client never sends money totals.
- Idempotency key is required on receive (client-generated GUID); missing key → 400.

## Definition of Done

- `dotnet build` clean; unit + integration green (totals math, transition guards, idempotent
  re-receive posts nothing twice, cross-tenant invisibility, supplier-in-use guard, concurrency
  → 409).
- Stack runs; E2E: admin supplier CRUD + draft → receive (stock deltas + movements) + same-key
  re-receive changes nothing + draft cancel; reader sees read-only purchasing.
- Frontend build + tests green. Docs updated, phase committed and pushed.
