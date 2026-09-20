# Phase 6 — Returns & Customers: implementation plan

> Scope per the PRD roadmap ("Phase 6 - Returns & Customers": customers, returns, refund
> handling), PRD §18 (sale lookup, item selection, returnable validation, reason, restock,
> refund, audit), §19 (optional customers, quick creation, search, history), §11
> (SALE_RETURN ledger), and the engineering rules (immutable sales → reversals, atomic
> completion, backend-authoritative money, tenant isolation).

## Motivation

Close the sale lifecycle without breaking the Phase 5 guarantees: completed sales stay
immutable records, corrections arrive as reversal documents. Cashiers need permitted
returns with server-computed refunds; the shop needs lightweight customers linked for
history without forcing accounts on walk-ins.

## Scope

### Backend

- Domain (`PharmaPOS.Domain.Customers`, `PharmaPOS.Domain.Sales`, tenant-scoped,
  `IsMultiTenant()`):
  - `Customer` — name, phone (unique per tenant when set), email, address, notes,
    active flag. Alternate key `(Id, TenantId)` for tenant-composite FKs.
  - `Sale.CustomerId` — optional link; receipt snapshot (`CustomerName/Phone`) stays
    authoritative and is auto-filled from the record when only the id is sent.
  - `SaleReturn` — sale FK (tenant-composite, restrict), reason, `Restock` toggle
    (default true), refund method/reference/amount (server-computed), actor, UTC stamp.
  - `SaleReturnItem` — one row per returned `SaleItem` (medicine/batch snapshots,
    quantity, unit price, pro-rata refund). Restocks target the original batch.
  - `SaleReturnIdempotencyKey` — per-tenant unique key → return (mirrors checkout).
- Return rules: sale must exist (tenant-scoped); lines must belong to the sale, appear
  once, quantity within `sold − alreadyReturned` → else 422 (`return.quantity_exceeded`);
  refund pro-rata from original line nets **including the header-discount share**, rounded
  4dp with remainder on the last row; cumulative refunds ≤ sale total → else 422;
  non-cash refunds require a reference; restock posts `SALE_RETURN` movements and batch
  increments in the same transaction (no restock = damaged path, stock untouched).
- Reads: customers list (search + active filter + paging), customer with recent sales;
  returns list (optional sale filter + paging), return detail, returnable-per-sale
  (sold/returned/returnable + net-per-unit + already-refunded).
- Checkout extension: optional `CustomerId` (must exist + be active; snapshot falls back
  to the record). Backward compatible: snapshot-only checkouts keep working.
- Permissions: `tenant.customers.read/manage`, `tenant.returns.read/manage`. Seed:
  Admin + Owner ← all four; Manager ← both reads; Cashier ← both reads + both manages
  (their permitted-returns + quick-create path); Inventory Staff ← none.
- Audit: `customer.created/updated/activated/deactivated/deleted`, `sale.returned`
  (sale, invoice, refund, method, restock). Replays are not re-audited.
- Migration `Phase6CustomersReturns`. No demo customers/returns seeded.

### Frontend

- `CustomersApi`, `ReturnsApi`; `SalesApi` gains `customerId`; nav **Customers**
  (`customers.read`); permission catalog extended.
- **Customers page**: search, create/edit form, activate/deactivate/delete (linked-sales
  block is surfaced), history view (recent linked sales).
- **POS**: customer search/select/clear + quick-create (gated by customers perms);
  checkout sends `customerId` plus the existing snapshot fields.
- **Sales page**: per-sale **Return** button (gated by returns read), returnable table
  with per-line qty inputs capped at returnable, reason + restock toggle + refund
  method/reference, server-computed refund estimate, process with per-return idempotency
  key; manage-gated submit.

## Explicitly out of scope (deferred, documented)

- Dues/credit ledger, loyalty, complex customer accounts (customer is a link, not credit).
- Purchase returns (`PURCHASE_RETURN` stays reserved), sale voids without line items.
- Real gateway refunds (manual recording per PRD; no fake "gateway" claims).
- Discount caps per role, thermal-printer formatting.

## Decisions

- Per-return (not per-line) `Restock` toggle, default true: covers damaged/expired
  exceptions per PRD "where applicable" with one explicit switch.
- New `tenant.returns.*` permissions (not reuse of sales): finer control, matches the
  user decision; cashiers hold both.
- Refunds server-computed pro-rata incl. header-discount share, capped at sale total:
  backend-authoritative, no client money trusted.
- Optional `CustomerId` + snapshot (not snapshot-only): clean history, backward compat.

## Definition of Done

- `dotnet build` clean; unit green (customer/return validators, permission catalog).
- Integration (needs Docker): restock + ledger, no-restock path, over/double-return
  guards, idempotent replay, customer link + history + returnable tracking, phone
  uniqueness, delete guard, cross-tenant invisibility for customers/returns/sales.
- Stack runs; E2E: search→link-customer→checkout→return→restock→refund; over-return
  refused; frontend build + tests green. Docs updated, phase committed and pushed.
