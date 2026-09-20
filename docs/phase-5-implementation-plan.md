# Phase 5 — POS & Sales: implementation plan

> Scope per the PRD roadmap ("Phase 5 - POS": search, barcode, cart, checkout, payments,
> receipt), PRD §14 (checkout workflow), §15 (cart incl. duplicate-line merging), §16
> (idempotency), §17 (payments: cash/card/bKash/Nagad/manual), §28 (receipt), and the critical
> engineering rules (atomic completion, backend-authoritative pricing, no negative inventory).

## Motivation

The biggest phase: turn the catalog + stock into money. Checkout must be fast for cashiers and
unbreakable under retries and concurrency: one atomic transaction per sale (sale + items +
payments + batch deductions + `SALE` movements + audit), idempotent per client key, FEFO batch
allocation that never touches expired stock, and prices resolved server-side from batches —
the client sends quantities, never money.

## Scope

### Backend

- Domain (`PharmaPOS.Domain.Sales`, tenant-scoped, `IsMultiTenant()`):
  - `Sale` — per-tenant sequential `InvoiceNumber` (`INV-000001…`, unique per tenant via an
    atomic counter row), UTC timestamp, server-computed subtotal/discount/total, optional
    customer snapshot (name/phone — no customer entity until Phase 6), optional prescription
    reference, cashier subject (server-resolved), `xmin` token. Completed sales are immutable
    records (voids arrive via returns in Phase 6).
  - `SaleItem` — tenant-composite FKs to sale/medicine/batch; quantity, server-resolved unit
    price (batch selling price at completion), line discount (capped at line gross), computed
    line total. One row per (line × batch): FEFO allocation may split a line across batches.
  - `SalePayment` — method (`Cash/Card/bKash/Nagad/Other`), amount, reference (required except
    cash), receiver (server-resolved). Split tender supported; paid must cover total; response
    reports change due.
  - `SaleIdempotencyKey` — per-tenant unique key → sale (mirrors the purchase pattern).
  - `InvoiceCounter` — per-tenant monotonic counter, incremented by atomic SQL in-transaction.
- Checkout rules: medicines must exist + be active; quantities positive; allocation FEFO across
  non-expired batches with stock (optional per-line batch override validated the same way);
  insufficient total → 422 (no partial sales); paid < total → 422; `xmin` conflicts → 409.
- Reads: sale list (paging + date range), sale detail, printable receipt DTO (profile + sale +
  items + payments + footer), today's summary (count + total).
- Permissions: `tenant.sales.read`, `tenant.sales.manage`. Seed: Admin + Owner ← both;
  Manager ← read; **Cashier ← read + manage** (their module lands now).
- Audit: `sale.completed` (total, lines, methods).
- Migration `SalesInitialCreate`. No demo sales seeded (stock/movements stay pristine).

### Frontend

- `SalesApi`; nav **POS** + **Sales** (`sales.manage` / `sales.read`).
- **POS page** (the flagship UI): search-as-you-type (brand/generic/manufacturer) + barcode
  fast-path (scanner = keyboard + Enter → exact lookup), results with Add, cart with qty
  steppers/line-discount/remove and duplicate-line merging, totals, multi-tender payment entry
  with change-due math, complete button (per-cart idempotency key, disabled while saving),
  receipt view with print stylesheet after completion.
- **Sales page**: history list + receipt reprint view.
- Dashboard today's-sales card.

## Explicitly out of scope (deferred, documented)

- Returns/refunds, sale voids (Phase 6 owns reversals; completed sales immutable).
- Customer entity, dues/credit ledger (Phase 6; checkout takes an optional name/phone snapshot).
- Real gateway integration (manual bKash/Nagad recording per PRD; no fake "gateway" claims).
- Discount caps per role (discounts recorded + audited; caps are a future policy).
- Thermal-printer formatting (print stylesheet now; ESC/POS later).

## Decisions

- Automatic FEFO allocation with optional per-line batch override (covers "confirm batch if
  required" without forcing it on every sale).
- Prices always from the batch at completion; client money fields (if ever sent) ignored.
- Invoice numbers sequential per tenant (counter row), never from the client.
- Idempotency key required on checkout; replay returns the stored sale + `X-Idempotent-Replayed`.

## Definition of Done

- `dotnet build` clean; unit + integration green (allocation order/expiry exclusion, totals +
  change math, insufficient-stock/concurrency guards, idempotent replay, cross-tenant
  invisibility, permission grants).
- Stack runs; E2E: admin search→cart→cash checkout→receipt→stock decremented→movements;
  over-quantity refused with a clear message; barcode scan resolves; dashboard today updates.
- Frontend build + tests green. Docs updated, phase committed and pushed.
