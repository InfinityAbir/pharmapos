# ADR-0003: Tenant resolution and membership model

- **Status:** Accepted
- **Date:** 2026-09-20
- **Phase:** 0 (implemented in Phase 1)

## Context

The bootstrap prompt fixes the tenancy flow:

```text
Keycloak → Authenticated User → Application User / Tenant Membership
         → Finbuckle.MultiTenant → Current Tenant Context
         → ASP.NET Core → EF Core tenant-aware filtering → PostgreSQL
```

and states that a `TenantId` supplied by the client must never be trusted.

## Decision

- **Finbuckle.MultiTenant owns resolution/context.** We do not invent a parallel mechanism.
- **Tenant metadata lives in the database** behind `IMultiTenantStore<TenantInfo>` (a custom
  `AppTenantInfo` subclass carries pharmacy metadata).
- **Resolution is derived from authenticated, server-side context:**
  - single-membership user → resolved from membership automatically;
  - multi-membership user → an explicit selection (validated against membership in the database)
    establishes context; an invalid/unauthorised selection returns `403` and never falls back.
  - URL segments, query strings, arbitrary headers and local storage are **not** tenant sources.
- **Membership model supports multiple tenants per user from day one:** `ApplicationUser`,
  `Tenant`, `TenantMembership` (unique on `(TenantId, ApplicationUserId)`), plus tenant-scoped
  `Role`/`Permission` and their join tables. V1 UX may default to one active pharmacy.
- **Authorization uses permissions**, resolved server-side for the current membership; client
  claims for roles/permissions are ignored.

## Consequences

- A user switching tenants is an explicit, auditable action.
- Tenant isolation depends on both Finbuckle context and EF filters plus DB constraints (ADR-0002).
- Membership is the joining point between identity (Keycloak) and tenancy, keeping the two concerns
  separate.
