# PharmaPOS

A multi-tenant SaaS point-of-sale platform for small and medium pharmacies, built with an
architecture and security posture intended for production from the first commit.

> **Current status: Phase 5 — POS & sales.** The solution builds, tests and runs end-to-end
> (Keycloak login → server-resolved membership → atomic idempotent checkout with FEFO batch
> allocation and receipts), but returns, localization and later phases are intentionally **not**
> implemented yet.

## What exists today

| Area | State |
| --- | --- |
| Clean Architecture solution (.NET 10) | Domain / Application / Infrastructure / Api |
| Multi-tenancy | Finbuckle.MultiTenant, shared database + `TenantId` discriminator, EF Core global query filters |
| Authentication | Keycloak 26 (OIDC), Authorization Code + PKCE (S256) SPA, bearer-only API |
| Authorization | Server-side permission policies (`HasPermission`) resolved per request from the database membership graph (`ApplicationUser → TenantMembership → Role → Permission`); token claims are ignored (ADR-0012) |
| Audit logging | Append-only, tenant-scoped audit log written via the application layer |
| API hardening | Explicit CORS allow-list, security headers, rate limiting, RFC 7807 error handling, correlation ids |
| Observability | Serilog structured logging, liveness/readiness health checks |
| Frontend | Angular 22 (zoneless, standalone components), authenticated shell, dashboard, audit log view, Tenants/Team/Roles and Medicines/Catalog-data/Pharmacy-profile administration with permission-gated navigation |
| Catalog | Tenant-scoped medicines (brand/generic/strength/form, per-tenant barcodes), categories, manufacturers, pharmacy profile |
| Inventory | Batch-level stock with expiry, derived totals, low-stock/expiry alerts, append-only movement ledger |
| Purchasing | Tenant-scoped suppliers, draft → received/cancelled purchases, atomic idempotent receiving into stock |
| Sales | Atomic idempotent checkout (FEFO allocation, server-side pricing, split tender, sequential invoices), history, printable receipts |
| Tests | 87 unit + 51 integration (real PostgreSQL via Testcontainers) + 11 frontend unit |

Detailed design lives in [`docs/architecture.md`](docs/architecture.md) and the decision records in
[`docs/adr/`](docs/adr/).

## Repository layout

```
src/
  PharmaPOS.Domain/          # Entities, value objects, domain errors — no dependencies
  PharmaPOS.Application/     # Use cases (MediatR), validation, abstractions
  PharmaPOS.Infrastructure/  # EF Core, multi-tenant store, Keycloak/auditing implementations
  PharmaPOS.Api/             # Composition root, auth pipeline, controllers, middleware
tests/
  PharmaPOS.UnitTests/
  PharmaPOS.IntegrationTests/   # Testcontainers PostgreSQL + Respawn
frontend/pharmacy-pos/       # Angular SPA
deploy/keycloak/             # Realm import (clients, mappers, demo identities)
docs/                        # Architecture, phase plan, ADRs
docker-compose.yml           # postgres + keycloak + api + web
```

## Prerequisites

- .NET SDK **10.0**
- Node.js **24+** and npm **11+** (for the SPA outside Docker)
- Docker with Compose v2 (the simplest way to run everything)
- Optional: `dotnet-ef` (`dotnet tool install --global dotnet-ef`)

## Run the whole stack

```powershell
Copy-Item .env.example .env      # then edit POSTGRES_PASSWORD / KEYCLOAK_ADMIN_PASSWORD
docker compose up --build
```

| Service | URL |
| --- | --- |
| Web SPA | http://localhost:4200 |
| API | http://localhost:8080 |
| API Swagger (Development) | http://localhost:8080/swagger |
| API health | http://localhost:8080/health · http://localhost:8080/health/ready |
| Keycloak admin console | http://localhost:8090 (realm `pharmapos`) |
| PostgreSQL | `localhost:5432` |

### Demo identities

Imported by Keycloak from [`deploy/keycloak/realm-export.json`](deploy/keycloak/realm-export.json).
Both belong to tenant `demo-pharmacy` but carry different permissions, which is enough to see the
authorization boundary in action.

| User | Password | Sees |
| --- | --- | --- |
| `admin@pharmapos.local` | `Platform#2026!` | `platform.tenants.manage`, `platform.users.manage`, `platform.audit.read` |
| `owner@demo-pharmacy.local` | `Pharmacy#2026!` | `platform.audit.read` |

These accounts exist for local development only and must never be imported into a real
environment.

## Build and test

```powershell
dotnet build PharmaPOS.slnx -c Release
dotnet test  PharmaPOS.slnx -c Release

cd frontend/pharmacy-pos
npm install
npm run build
npm test -- --watch=false
```

The integration tests start an ephemeral PostgreSQL container, so Docker must be running.

## Security model (summary)

- **Keycloak owns the token lifecycle.** Access tokens are short-lived; refresh token rotation and
  reuse detection are Keycloak features. The API only **validates** tokens — it never issues,
  refreshes or rotates them.
- **Tenant identity is never taken from the client.** `ClaimsTenantStrategy` reads the validated
  `tenant_id` claim; EF Core applies tenant filters, and the multi-tenant context rejects writes
  that carry a foreign `TenantId`.
- **Authorization is re-evaluated server-side** on every request. Token claims describe *who* the
  caller is; they are not trusted as an authorization outcome by themselves.
- **Fail-safe defaults.** Configuration is validated at startup, CORS is an explicit allow-list,
  errors never leak internals, and security-relevant rejections are logged with a correlation id.

See [`docs/architecture.md`](docs/architecture.md) for the full picture.

## Documentation

- [`docs/architecture.md`](docs/architecture.md) — system architecture and tenancy model
- [`docs/phase-0-implementation-plan.md`](docs/phase-0-implementation-plan.md) — scope of this phase
- [`docs/development.md`](docs/development.md) — local workflow, migrations, troubleshooting
- [`docs/adr/`](docs/adr/) — architecture decision records

## License

See [`LICENSE`](LICENSE).
