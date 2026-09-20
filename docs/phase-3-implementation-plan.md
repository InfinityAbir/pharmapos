# Phase 3 — Inventory: implementation plan

> Scope per the PRD roadmap ("Phase 3 - Inventory": batch, stock, inventory movement, expiry,
> low-stock), PRD §10 (batch & expiry), §11 (inventory movements), and architecture §4
> (tenant-composite keys, `xmin` on mutable rows, atomic stock updates, UTC).

## Motivation

The catalog (Phase 2) defines *what can be sold*. Phase 3 tracks *what is on hand*: batch-level
quantities with expiry, an append-only movement ledger, and low-stock / expiry alerts. Sales
deduction (Phase 5), purchase receiving workflow (Phase 4) and returns (Phase 6) will write the
same ledger through new operations — this phase establishes the ledger, the invariants, and the
manual operations (receive / adjust / expire) that also serve opening stock.

## Scope

### Backend

- Domain (`PharmaPOS.Domain.Inventory`, all tenant-scoped, `IsMultiTenant()`):
  - `MedicineBatch` — tenant-composite FK to `Medicine`, `UNIQUE (TenantId, MedicineId,
    BatchNumber)`, batch purchase/selling price (`numeric(18,4)`), integer `Quantity`,
    `ManufactureDate`/`ExpiryDate` (`DateOnly`), nullable free-text `SupplierName` (supplier
    entity arrives in Phase 4), `xmin` row-version token.
  - `InventoryMovement` — append-only: medicine + batch refs (tenant-composite FKs), `Type`
    (`OPENING_STOCK`, `PURCHASE_RECEIVED`, `ADJUSTMENT_IN/OUT`, `EXPIRED`; `SALE*`,
    `PURCHASE_RETURN`, `SALE_RETURN` reserved for later phases), signed `QuantityChange`,
    `QuantityAfter`, reason, actor, UTC timestamp. No update/delete surface.
- Stock is **derived, never dual-written**: medicine totals = `SUM(batches.Quantity)`.
  Low-stock = total below the medicine's `ReorderLevel`.
- Operations (all transactional: batch write + movement + audit in one `SaveChanges`):
  - Receive (`manage`): new batch, or top-up by (medicine, batch number); past expiries rejected.
  - Adjust (`manage`): signed change + reason; result must stay ≥ 0 (422 otherwise).
  - Expire (`manage`): quantity → 0 with an `EXPIRED` movement recording the removed qty.
  - `DbUpdateConcurrencyException` → `409 batch.concurrent_update` (never a bare 500).
- Reads (`read`): batch list (medicine filter, expiring-within-days, include-expired), per-medicine
  stock detail, low-stock list, expiring/expired lists, movement log (medicine/batch filter,
  paging), alerts summary (low-stock count, expiring-≤30d count, expired count).
- Permissions: `tenant.inventory.read`, `tenant.inventory.manage`. Seed: Platform Admin + Owner ←
  both; Manager ← read; **Inventory Staff ← read + manage** (their module lands now).
- Audit: `stock.received`, `stock.adjusted`, `batch.expired`.
- Migration `InventoryInitialCreate` + idempotent seed: opening-stock batches for the demo
  medicines (incl. one near-expiry batch, one low-stock medicine, one zeroed expired batch with
  its `EXPIRED` movement).

### Frontend

- `InventoryApi`; nav entry **Inventory** (`inventory.read`).
- One **Inventory** page: medicine stock table (search, low-stock-only toggle, per-row total +
  flags) → selecting a medicine shows its batches with receive/adjust/expire actions (gated by
  `inventory.manage`) and its recent movements.
- Dashboard alerts card (low-stock / expiring / expired counts) for readers.

## Explicitly out of scope (deferred, documented)

- Purchase workflow, supplier entity/linking, sale deduction, returns (Phases 4–6 write new
  movement types through new operations).
- Stock transfers between locations (single-location V1; PRD lists it as "where applicable").
- Automated notifications (architecture lists notifications at 3+; alerts are queryable lists in
  this phase, no push/email).
- Barcode-label printing (still future per PRD §27).

## Decisions

- Integer quantities (pharmacy retail units are countable); fractional support only if a real
  case demands it.
- Expiry required on receive; past expiry rejected — receiving expired stock is never valid.
- Medicines are deactivated, never deleted, so batch history is never orphaned.

## Definition of Done

- `dotnet build` clean; unit + integration green (per-tenant batch-number uniqueness,
  no-negative-stock guard, expiry rules, concurrency conflict → 409, cross-tenant invisibility,
  movement ledger completeness for every mutation).
- Stack runs; E2E: admin receives/adjusts/expires stock and sees alerts move; reader sees
  read-only inventory; seed alerts visible on dashboard.
- Frontend build + tests green. Docs updated, phase committed and pushed.
