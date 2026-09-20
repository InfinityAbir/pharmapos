# Phase 7 — Dashboard & Reports: implementation plan

> Scope per the PRD roadmap ("Phase 7 - Dashboard & Reports") and the server-side
> aggregation mandate: the database does the heavy processing, the API returns compact
> results, Angular renders. No premature optimization (no caching, materialized views,
> or background aggregation — properly indexed EF Core queries first, measure later).

## Motivation

Phases 0–6 built the operational modules; their data now needs to answer business
questions without ever shipping raw ledgers to the browser. Every figure on the
dashboard and reports comes from a PostgreSQL aggregate over a tenant-filtered,
indexed set.

## Scope

### Backend (all reads, all server-side)

- New `ReportsController` (`api/v1/tenant/reports`), new `IReportReader`/`ReportReader`,
  one MediatR query per report. Existing readers and endpoints are untouched except two
  targeted optimizations below. No new permissions: sales reports → `tenant.sales.read`,
  inventory → `tenant.inventory.read`, purchasing → `tenant.purchasing.read`.
- Sales: `trend` (day/week/month buckets via `date_trunc` + `AT TIME ZONE` in SQL, tenant
  timezone with UTC fallback), `by-cashier`, `by-method`, `by-medicine` — all SQL
  `GROUP BY`/`SUM`/`COUNT` with narrow projections and bounded ranges (default 30d,
  max 366d, take ≤ 100). Figures are gross, consistent with today's summary.
- Products: `performance` (trailing-days window, best/slow switch; slow includes
  zero-sale actives via an outer aggregate in one statement). Feeds best-/slow-seller
  reports and the dashboard top-sellers card.
- Inventory: `valuation` (portfolio totals + per-medicine rows in SQL), `low-stock`
  (grouped batch sums vs reorder in SQL), `movements` (type + date filters, paged,
  narrow projection). Expiring/expired lists are reused as-is (bounded stocked-batch
  sets; alert counts were already `COUNT` queries).
- Purchasing: `by-supplier` (received only), `totals` (received count/total in range +
  open drafts). `PurchaseDate` is a tenant-local date column: direct comparison.
- Targeted optimizations of working code (same contracts, covered by existing tests):
  `SaleReader` today summary → `COUNT`/`SUM` instead of loading the day's rows;
  `StockReader` alerts low-stock count → grouped SQL count instead of the full
  catalog load. Raw trend SQL carries an explicit tenant predicate (global filters do
  not apply to raw SQL) and constant statements per bucket (no injection surface).
- Migration `Phase7ReportingIndexes`: `sales (TenantId, CashierSubject)`,
  `sale_payments (TenantId, Method)`,
  `purchases (TenantId, Status, PurchaseDate)`. All other group/filter columns were
  already covered by tenant-leading indexes.

### Frontend (render only)

- `ReportsApi`; **Reports** page (auth-guarded route, per-section permission gating):
  date filters + trend bucket + movers window, one section per report with
  loading/empty/error states, client-side CSV export of the displayed aggregates.
  No totals, averages, or rankings are computed in Angular. PDF export out of scope.
- Dashboard: new top-sellers (30d) and recent-sales cards (existing list endpoint),
  both fed by server aggregates; today's transactions/revenue, alerts, and purchase
  summary already were.

## Explicitly out of scope (deferred, documented)

- Net-of-refunds revenue, caching/materialized views/background rollups (measure first).
- Server-side CSV/PDF endpoints, scheduled report delivery, custom report builder.

## Definition of Done

- `dotnet build` clean, zero warnings; unit green (report validators, permission
  catalog, CSV builder); integration green where Docker is available (aggregate math
  per report, cross-tenant invisibility, range/paging caps).
- Frontend build + tests green. Docs updated, phase committed and pushed.
