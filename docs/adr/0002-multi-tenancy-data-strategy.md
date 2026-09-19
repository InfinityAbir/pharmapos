# ADR-0002: Multi-tenancy data strategy — shared database, TenantId discriminator

- **Status:** Accepted
- **Date:** 2026-09-20
- **Phase:** 0 (implemented in Phase 1)

## Context

Tenants are pharmacies. V1 serves small/medium businesses; per-tenant databases would add
operational cost and migration complexity without a demonstrated isolation need. Data leakage between
tenants is nevertheless unacceptable.

## Decision

Use a **shared PostgreSQL database with shared schema and a `TenantId` discriminator**, and enforce
isolation in depth:

1. Tenant-owned entities implement `IMultiTenant` (`string TenantId`).
2. EF Core **global query filters** scope all reads to the current tenant; `SaveChanges` stamps
   `TenantId` on insert and forbids changes on update.
3. **Database constraints** back the application filter:
   - tenant-owned foreign keys include `TenantId` so cross-tenant references cannot be stored;
   - unique constraints include `TenantId`;
   - indexes are tenant-leading.
4. Platform-owned tables (`Tenant`, `Subscription`, `Permission` catalog, platform audit) are
   explicitly exempt and documented as such.

## Alternatives considered

- **Database per tenant** — stronger isolation, rejected for V1 on operational/migration cost and
  because PRD §7 explicitly calls for shared tables with `TenantId` discrimination.
- **Schema per tenant** — rejected as a middle ground with the worst of both maintenance profiles.

## Consequences

- Every query must flow through tenant-context-aware EF Core; raw SQL is discouraged and reviewed.
- Tenant isolation tests are mandatory and are a release gate.
- Future multi-branch support adds a `BranchId` scoping dimension; the tenant-scoping foundation
  does not block it.
