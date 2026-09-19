# PharmaPOS — Architecture Analysis

> Status: **Phase 0 — accepted as the baseline design.**
> This document is the first deliverable required by the coding-agent bootstrap prompt.
> It is intentionally high-level; per-decision detail lives in `docs/adr/`.

## 0. Context and constraints

- Multi-tenant **SaaS POS** for small/medium pharmacies in Bangladesh (V1 vertical: pharmacy).
- **One extensible platform + industry modules** — the core must not be pharmacy-specific.
- Stack is fixed by the bootstrap prompt: ASP.NET Core Web API, EF Core, PostgreSQL,
  Clean Architecture, CQRS/MediatR, FluentValidation, Keycloak, Finbuckle.MultiTenant,
  Angular + TypeScript, Docker/Docker Compose, xUnit.
- **Backend is authoritative.** Tenant, price, discount, total, stock, roles and permissions are
  never trusted from the client.
- **No fake functionality.** Unimplemented features are visibly unavailable, never stubbed.

## 1. High-level architecture

```text
                         ┌────────────────────────────┐
   Browser (cashier)     │  Angular SPA (pharmacy-pos)│
                         └──────────────┬─────────────┘
                                        │ HTTPS / JSON  /api/v1/**
                                        │ Bearer JWT (Keycloak)
                         ┌──────────────▼─────────────┐
                         │      Reverse proxy          │  (nginx / ingress)
                         └──────────────┬─────────────┘
                                        │
        ┌───────────────────────────────▼───────────────────────────────┐
        │                        ASP.NET Core API                        │
        │  Api ──▶ Application ──▶ Domain                                │
        │    ▲            │                                              │
        │    └── Infrastructure ──▶ EF Core / Keycloak / storage         │
        └───────┬───────────────────────────┬───────────────────┬──────┘
                │                           │                   │
        ┌───────▼───────┐           ┌───────▼────────┐   ┌──────▼───────┐
        │  PostgreSQL   │           │    Keycloak    │   │ Background   │
        │ shared DB,    │           │  (OIDC, JWT)   │   │ workers (job)│
        │ TenantId disc.│           └────────────────┘   └──────────────┘
        └───────────────┘
```

### Runtime request pipeline (authoritative order)

```text
Request
 → Serilog request logging
 → Correlation id
 → Exception handler (RFC 7807 ProblemDetails)
 → HTTPS / CORS
 → Authentication        (validates Keycloak JWT; establishes the security principal)
 → MultiTenant resolution (Finbuckle) — resolves + validates tenant from server-side context
 → Authorization         (permission policies; tenant membership enforced)
 → Rate limiting
 → Endpoint (thin) → MediatR
      → Logging behavior → Validation behavior (FluentValidation) → Transaction behavior
      → Handler → Domain → EF Core (tenant-filtered) → PostgreSQL
 → Health checks (/health, /health/ready)
```

`UseMultiTenant()` runs **after** authentication so tenant resolution can read the authenticated
principal. Tenant selection is validated against database membership — never against a raw client
value (see ADR-0003).

## 2. Repository structure

```text
pharmapos/                              # git repo root
├── src/
│   ├── PharmaPOS.Domain/               # entities, value objects, domain events, domain rules
│   ├── PharmaPOS.Application/          # use cases (MediatR), validators, DTOs, abstractions
│   ├── PharmaPOS.Infrastructure/       # EF Core, tenant store, Keycloak, external services
│   └── PharmaPOS.Api/                  # HTTP host: endpoints, DI composition, middleware
├── frontend/
│   └── pharmacy-pos/                   # Angular application
├── tests/
│   ├── PharmaPOS.UnitTests/
│   ├── PharmaPOS.IntegrationTests/     # PostgreSQL via Testcontainers, tenant isolation
│   └── PharmaPOS.E2ETests/             # Playwright
├── deploy/
│   ├── docker/                         # Dockerfiles
│   ├── compose/                        # compose overrides
│   └── keycloak/                        # realm import
├── docs/
│   ├── architecture.md                 # this file
│   ├── phase-0-implementation-plan.md
│   ├── development.md
│   ├── adr/                            # Architecture Decision Records
│   └── reference/                      # PRD + bootstrap prompt (source of truth)
├── scripts/                            # dev/ops helper scripts
├── .github/workflows/                  # CI
├── Directory.Build.props               # shared MSBuild settings
├── Directory.Packages.props            # central package version management
├── docker-compose.yml
├── .env.example
├── README.md
└── LICENSE
```

A separate `SharedKernel` project is **not** introduced yet — there is no concrete cross-cutting
type that justifies it (Rule 6: avoid premature abstraction). Domain primitives start inside
`Domain`.

## 3. Domain / module boundaries

The platform core is vertical-agnostic; pharmacy is a module.

### Shared SaaS core (vertical-agnostic)

| Module | Responsibility | Phase |
|---|---|---|
| Identity & Tenancy | users, memberships, roles, permissions, tenant context | 1 |
| Pharmacy/Organization profile | tenant profile/settings | 2 |
| Catalog (generic product) | products, categories, units, pricing | 2 |
| Inventory | stock, batches/lots, movements | 3 |
| Parties | suppliers, customers | 4, 6 |
| Purchasing | purchase orders, receiving | 4 |
| Sales / POS | cart, checkout, payments, receipts | 5 |
| Returns | sale/purchase returns | 6 |
| Reporting | dashboard + reports | 7 |
| Audit | append-only audit events | 1+ (written by all modules) |
| Notifications | low stock, expiry, failures | 3+ |

### Pharmacy vertical module (V1)

- `Medicine` as the concrete catalog entity (brand/generic, strength, dosage form, Rx flag).
- Batch/lot + expiry semantics (mandatory, unlike generic retail).
- Expiry-aware selling and near-expiry reporting.

### Boundary rules

- Domain has **no** dependency on Application/Infrastructure/frameworks.
- Application depends only on Domain (+ MediatR/FluentValidation abstractions).
- Infrastructure depends on Application (implements its interfaces) and Domain.
- Api depends on Application + Infrastructure (composition root only).
- Modules communicate through **application abstractions / integration events**, never by reaching
  into each other's EF entities. Cross-module data access goes through queries/DTOs.

## 4. Database strategy (V1)

**Shared PostgreSQL database, shared schema, `TenantId` discriminator** (see ADR-0002).

- Every tenant-owned entity implements `IMultiTenant` (`string TenantId`).
- EF Core **global query filters** scope every query to the current tenant; `SaveChanges`
  stamps `TenantId` on insert and rejects mismatches on update.
- **Belt-and-braces**: database-level constraints back the filters:
  - FKs between tenant-owned tables include `TenantId` to make cross-tenant references structurally
    impossible (composite alternate keys).
  - Unique constraints are composite and include `TenantId`
    (e.g. `UNIQUE (TenantId, Barcode)`).
  - Indexes are tenant-leading: `(TenantId, ...)`.
- Not every table is tenant-owned. **Platform-owned** tables (`Tenant`, `Subscription`,
  `Permission` catalog, platform `AuditLog` for super-admin actions) are explicitly marked and are
  the only ones excluded from the tenant filter.
- **Transactions**: sale completion, purchase receiving and returns run inside a single DB
  transaction (`IUnitOfWork`/`ITransactionBehavior`).
- **Concurrency**: optimistic concurrency via PostgreSQL `xmin` on mutable stock/aggregate rows,
  plus atomic conditional updates (`UPDATE ... WHERE quantity >= @qty`) so concurrent cashiers
  cannot drive stock negative. Frontend validation is never relied upon.
- **Idempotency**: an `IdempotencyKey` record (tenant-scoped, unique) guards critical commands
  (sale completion, purchase receiving, returns). A repeated key returns the stored outcome
  instead of re-executing.
- **Migrations**: EF Core migrations, generated in `Infrastructure`, applied by an explicit
  migration step (not silently at startup in production). Seeding is idempotent and environment
  aware.
- Money uses `numeric(18,4)`; currency is BDT by default. Never `float`/`double`.
- All timestamps stored in UTC (`timestamptz`); tenants render in their configured timezone.

## 5. Keycloak strategy

See ADR-0004.

- **One realm per environment** (`pharmapos`), configured via importable realm JSON
  (`deploy/keycloak/realm-pharmapos.json`).
- **Clients**
  - `pharmapos-web` — public SPA client, Authorization Code + PKCE, no client secret.
  - `pharmapos-api` — bearer-only/resource server, confidential where a secret is needed.
- **Token validation** — the API validates access tokens against the realm JWKS
  (`Authority` + `Audience`); issuer/audience/lifetime/signature are enforced.
- **External identity linked to an application user.** Keycloak owns credentials; the application
  owns authorization. `ApplicationUser.KeycloakSubject` maps the `sub` claim to a local user.
- **Roles vs permissions**: realm roles are used only for platform-level concerns (e.g.
  `platform-admin`). Business roles and their permissions are **stored in the application database**
  and resolved server-side per tenant. Client-supplied roles/permissions are ignored.
- Keycloak administration (tenant/user provisioning) happens through the Keycloak Admin API from a
  dedicated application service, not from the browser.

## 6. Finbuckle.MultiTenant strategy

See ADR-0003.

- Finbuckle provides the **tenant resolution/context** mechanism; we do not build custom resolution
  infrastructure.
- `TenantInfo` (or a subclass `AppTenantInfo` carrying pharmacy metadata) is stored in the
  application database behind `IMultiTenantStore<TenantInfo>` — multi-tenancy metadata lives in the
  DB like all other application data.
- **Resolution must be server-derived.** The active tenant comes from the authenticated
  principal's tenant membership, not from a URL segment, query string, or arbitrary header.
  - A user belonging to exactly one tenant resolves directly.
  - A user belonging to multiple tenants must explicitly select one; the selection is a *hint* that
    is **validated against membership** before it becomes tenant context. An unvalidated selection
    yields `403`, never a fallback to another tenant's data.
- `MultiTenantDbContext` + `[MultiTenant]` entities wire EF Core filtering automatically once
  Phase 1 introduces the tenant store.

## 7. Tenant & membership model

See ADR-0003. Design goal: **a user may belong to multiple tenants**, even if V1 UX has one active
pharmacy.

```text
ApplicationUser (platform-level, 1 row per human)
   id, keycloak_subject (unique), email, display_name, is_active, created_at

Tenant (Finbuckle TenantInfo subclass; platform-owned)
   id (string, e.g. "rahman-pharmacy"), identifier, name, status, plan_ref

TenantMembership (join: user ⇄ tenant)
   id, tenant_id, application_user_id, status, is_default, joined_at
   UNIQUE (tenant_id, application_user_id)

Role (tenant-scoped)
   id, tenant_id, name, is_system
   UNIQUE (tenant_id, name)

Permission (platform-defined catalog)
   id, code (e.g. "sale.complete"), description

RolePermission (role ⇄ permission)   UNIQUE(role_id, permission_id)
MembershipRole  (membership ⇄ role)   UNIQUE(membership_id, role_id)
```

- Authorization evaluates **permission codes** for the current membership, computed server-side.
- V1 roles seeded per tenant: Owner, Manager, Cashier, Inventory Staff.
- Multi-branch is future-proofed by keeping everything tenant-scoped and introducing `BranchId`
  later as an additional scoping dimension (see ADR-0002 "future" note); no decision here blocks it.

## 8. Angular architecture

See ADR-0007.

- **Angular 22**, standalone components, strict TypeScript, SCSS.
- Feature-first structure; `core/` (singletons: auth, http, config, layout) and `shared/`
  (reusable presentational pieces).
- **State**: start with Angular signals + thin signal stores per feature. No NgRx until a concrete
  need appears (avoid premature complexity; mirrors the backend Rule 6).
- **Auth**: `angular-auth-oidc-client` against Keycloak (Authorization Code + PKCE). A functional
  `authGuard` protects authenticated routes; the HTTP interceptor attaches the access token.
- **API layer**: a typed `ApiClient` over `HttpClient`, base URL from environment config, plus:
  - auth interceptor (token attach + 401 handling),
  - error interceptor (maps ProblemDetails/validation errors to a consistent UI shape),
  - correlation id header.
- **UX foundation**: global loading indicator, toast/notification service, and consistent empty/
  error states.
- **Localization** (Phase 8): `@ngx-translate` with `en`/`bn`; Bangla receipts and Unicode-safe
  fonts handled in the receipt feature.
- **Checkout is a first-class, non-CRUD screen** (Phase 5): keyboard/barcode first, minimal typing,
  fast search, clear totals, fast payment.

## 9. Testing architecture

See ADR-0008.

| Layer | Tooling | Scope |
|---|---|---|
| Domain/application unit | xUnit + FluentAssertions 7 | pricing, discounts, stock, expiry, returns, permissions, validators |
| Pipeline unit | xUnit | MediatR behaviors, exception mapping, tenant filtering logic |
| Integration | xUnit + `WebApplicationFactory` + Testcontainers PostgreSQL + Respawn | migrations, real PostgreSQL, transactions, concurrency, **tenant isolation** |
| API | `WebApplicationFactory` | auth, authorization, validation, ProblemDetails, pagination |
| E2E | Playwright | `Login → Tenant → Medicine → Cart → Payment → Receipt → Inventory` and purchase→receive→sale flows |

**Mandatory:** automated tests proving Tenant A cannot read or mutate Tenant B data, and that
concurrent sales cannot oversell/oversell-negative stock. These are Phase-1/Phase-5 gates, and the
isolation harness is scaffolded in Phase 0.

## 10. Cross-cutting concerns

- **Validation**: FluentValidation, executed by a MediatR pipeline behavior before handlers; a
  consistent `ValidationProblemDetails` response.
- **Errors**: `IExceptionHandler` → RFC 7807 `ProblemDetails`; domain exceptions map to stable
  status codes and error codes.
- **Logging**: Serilog structured logging, correlation id enrichment, request logging.
- **Observability**: health checks (`/health`, `/health/ready`) now; OpenTelemetry/Prometheus/Grafana
  later without over-engineering the MVP (PRD §44).
- **Config & secrets**: strongly-typed options, validated at startup; secrets from environment or a
  secret store, never committed. `.env.example` documents required variables.
- **Security**: HTTPS, strict CORS, rate limiting on auth-heavy endpoints, EF Core parameterisation,
  append-only audit, no client-trusted business values.

## 11. Delivery sequence (phases)

0. Foundation (this doc + runnable skeleton) → 1. Identity & tenancy → 2. Pharmacy & medicine →
3. Inventory/batch/expiry → 4. Suppliers & purchasing → 5. POS & payments → 6. Customers & returns →
7. Dashboard & reports → 8. Bangla/English → 9. Offline & hardening → 10. Production.

Each phase: **Understand → Design → Implement → Test → Review**, with an E2E check before the phase
is committed and pushed.
