# Phase 2 — Pharmacy & Medicine: implementation plan

> Scope per the PRD roadmap ("Phase 2 - Pharmacy & Medicine": pharmacy profile, medicine,
> category, manufacturer, barcode), PRD §8 (profile), §9 (medicine fields/search), §27 (barcode),
> and architecture §3–§4 (catalog module, tenant-composite keys, `numeric(18,4)` money, UTC).

## Motivation

Phase 1 proved database-backed authorization. Phase 2 builds the first business module on it: the
tenant's pharmacy profile and medicine catalog. Stock, batches and expiry are Phase 3 — the
catalog carries prices and reorder levels now so POS (Phase 5) has something to sell, but no
quantities live here.

## Scope

### Backend

- Domain (`PharmaPOS.Domain.Catalog`, all tenant-scoped, `IsMultiTenant()`):
  - `Manufacturer` — `UNIQUE (TenantId, Name)`.
  - `MedicineCategory` — `UNIQUE (TenantId, Name)`.
  - `Medicine` — brand/generic/strength/dosage-form, nullable tenant-composite FKs to
    manufacturer and category, `Barcode` (unique per tenant when present), unit, pack size,
    purchase/selling price (`numeric(18,4)`, ≥ 0), reorder level, nullable VAT rate,
    Rx flag, active flag, `xmin` row-version concurrency token.
  - `PharmacyProfile` — one row per tenant (`UNIQUE (TenantId)`): name, address, phone, email,
    website, registration/VAT info, invoice footer, currency (default `BDT`), language,
    timezone, nullable `LogoUrl` (upload deferred).
- Permission catalog additions (`tenant.*` namespace, per convention):
  - `tenant.medicines.read` — search/list/get medicines, list categories/manufacturers, read profile.
  - `tenant.medicines.manage` — full catalog CRUD incl. barcode assignment.
  - `tenant.pharmacy.manage` — pharmacy profile write.
- Endpoints (all under `/api/v1/tenant`, tenant-filtered by Finbuckle):
  - `GET medicines?search=&categoryId=&manufacturerId=&isActive=&skip=&take=` (read; ILIKE on
    brand/generic/barcode + manufacturer name; `take` capped at 100).
  - `GET medicines/{id}`, `GET medicines/by-barcode/{barcode}` (read; exact barcode match, POS-ready).
  - `POST medicines`, `PUT medicines/{id}`, `PATCH medicines/{id}/status` (manage).
  - Categories / manufacturers: list + create + rename + delete (manage; delete → 409 when referenced).
  - `GET pharmacy-profile` (read), `PUT pharmacy-profile` upsert (pharmacy.manage).
- Audit events: `medicine.created/updated/status.changed/price.changed` (price event only when a
  price actually differs), `category.*`, `manufacturer.*`, `pharmacy-profile.updated`.
- Migration `CatalogInitialCreate` + idempotent seed: demo categories, Bangladeshi manufacturers
  (Square, Beximco, Incepta), a handful of demo medicines with barcodes, demo-pharmacy profile.
- Seed permission updates: `Platform Admin` ← all three; `Owner` ← read + manage + pharmacy.manage;
  `Manager` ← read (first real grant for the previously permission-less V1 roles; Cashier and
  Inventory Staff stay empty until POS/inventory land).

### Frontend

- `CatalogApi` + permission-gated nav (`medicines.read`).
- Pages: **Medicines** (search-as-you-type, list, create/edit form with category/manufacturer
  selects, barcode field, Rx toggle, active toggle) guarded read/manage respectively;
  **Catalog data** (categories + manufacturers CRUD panels); **Pharmacy profile** (form).
- Dashboard shows catalog counts (medicines, categories, manufacturers).

## Explicitly out of scope (deferred, documented)

- Batches, stock quantities, movements, expiry (Phase 3). Catalog prices are the selling reference
  until batch pricing arrives.
- Barcode generation/printing (PRD lists label printing as a future enhancement); scanners work as
  keyboard input against the barcode field + `by-barcode` lookup.
- Logo upload (profile stores `LogoUrl` only).
- Medicine hard-delete (deactivate only — future sales will reference medicines). Category /
  manufacturer delete is refused while referenced.
- pg_trgm fuzzy search (substring ILIKE now; revisit with real catalog volume).

## Decisions

- New permissions + seed grants as above (ADR-0013 if the shape proves contentious; else folded
  into the phase commit notes).
- `DosageForm` validated against a fixed list + `Other` (data quality without dead-ends).
- Prices required and ≥ 0; VAT rate nullable 0–100; pack size nullable positive.

## Definition of Done

- `dotnet build` clean; unit + integration green (barcode uniqueness per tenant, cross-tenant
  invisibility, category/manufacturer referential guards, profile upsert, permission grants).
- Stack runs; E2E: admin manages catalog end-to-end; owner manages catalog but (unchanged) not
  tenants; a catalog search by barcode resolves the medicine.
- Frontend build + tests green. Docs updated, phase committed and pushed.
