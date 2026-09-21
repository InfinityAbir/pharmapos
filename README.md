<p align="center">
  <img src="https://img.shields.io/badge/Docker-Compose-2496ED?style=for-the-badge&logo=docker&logoColor=white" alt="Docker" />
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Angular-22-DD0031?style=for-the-badge&logo=angular&logoColor=white" alt="Angular" />
  <img src="https://img.shields.io/badge/PostgreSQL-17-4169E1?style=for-the-badge&logo=postgresql&logoColor=white" alt="PostgreSQL" />
  <img src="https://img.shields.io/badge/Keycloak-26-4D4D4D4?style=for-the-badge&logo=keycloak&logoColor=white" alt="Keycloak" />
</p>

<p align="center"><b>A modern, fast, and secure multi-tenant POS SaaS — Bengali-first, built for pharmacies in Bangladesh.</b></p>

<p align="center">POS checkout • Batch/expiry inventory • Purchasing • Returns • Reports • Offline-safe sales • Bangla/English</p>

---

## Table of Contents

- [About the Project](#about-the-project)
- [Why I Built PharmaPOS](#why-i-built-pharmapos)
- [Who Will Use It](#who-will-use-it)
- [Why They Will Use It](#why-they-will-use-it)
- [Key Features](#key-features)
- [Tech Stack](#tech-stack)
- [Architecture Overview](#architecture-overview)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [Security](#security)
- [Project Structure](#project-structure)
- [Roadmap](#roadmap)
- [Contributing](#contributing)
- [License](#license)
- [Author & Contact](#author--contact)

---

## About the Project

**PharmaPOS** is a production-oriented, multi-tenant point-of-sale SaaS platform built specifically for **small and medium-sized pharmacies in Bangladesh**.

Most existing POS software is either a generic retail tool that knows nothing about batches and expiry dates, an English-only interface staff struggle with, or a single-shop desktop app that can't serve a SaaS customer base. PharmaPOS brings everything together in one clean, fast, secure place:

> Barcode-first checkout, batch/expiry-aware inventory with FEFO allocation, supplier purchasing, customers and returns, tenant-scoped dashboard and reports, offline-safe sales queue, and a full Bangla/English UI — all behind Keycloak authentication with strict per-tenant isolation.

Built with **ASP.NET Core (.NET 10) + Angular 22**, **PostgreSQL**, **Finbuckle.MultiTenant**, and **Keycloak 26**, fully containerized with separate dev and production Compose stacks.

Status: **Phase 10 — Production & Deployment.** The solution builds, tests and runs end-to-end (see `docs/production.md` before deploying anywhere real).

---

## Why I Built PharmaPOS

I built PharmaPOS to solve problems I saw in Bangladeshi pharmacies firsthand:

1. **Pharmacy is not generic retail:** Tablets have strengths and dosage forms, stock lives in batches with expiry dates, and selling an expired strip is a safety issue — not just an accounting error. Generic POS systems model none of this. PharmaPOS is pharmacy-first: batches, expiry gating, FEFO allocation, prescription flags.
2. **Language barrier:** Many pharmacy staff work most comfortably in Bangla. Every screen, validation message and receipt is available in বাংলা alongside English, with ৳ formatting throughout.
3. **Trust through correctness:** A sale must deduct stock exactly once — no double-charges on double-clicks, no negative inventory when two cashiers sell the last strip at the same time. Checkout is atomic and idempotent, enforced by the database, not by frontend hope.
4. **One tenant must never see another:** In a SaaS POS, a data leak between pharmacies is fatal. Tenant context is resolved server-side from the authenticated identity, filtered on every query, and proven by automated isolation tests — never taken from client input.
5. **Internet goes down, the shop doesn't:** Checkout intents queue locally and replay idempotently when connectivity returns, so a temporary outage never loses a sale or creates a duplicate.

This is also my exploration of production-grade SaaS engineering: Clean Architecture, CQRS, Keycloak OIDC, multi-tenancy, EF Core concurrency control, offline sync, and hardened Docker deployments.

---

## Who Will Use It

- **Independent and neighborhood pharmacies** in Bangladesh that need fast billing, expiry tracking and simple reports without ERP complexity.
- **Cashiers** who need a keyboard/barcode-first checkout: scan, tap, pay, print — with minimal typing.
- **Pharmacy owners and managers** who want purchase receiving, stock alerts, sales reports and staff permission control per tenant.
- **Inventory staff** receiving supplier deliveries into batch-tracked stock with manufacturing/expiry dates.
- **Small pharmacy chains** that need strict per-shop data isolation today and a model that can grow branches tomorrow.

The app is fully bilingual (বাংলা / English toggle) so it also works for English-preferring staff.

---

## Why They Will Use It

- **Bengali-first, not translated as an afterthought:** Every screen, cart total, validation message and receipt renders in Bangla with Bengali digits and ৳ formatting, alongside English.
- **Checkout built for speed:** Search-as-you-type, barcode scan, cart merge rules, discounts, split tender (Cash / Card / bKash / Nagad), sequential invoices and printable receipts.
- **Inventory you can trust:** Batch-level stock, near-expiry and low-stock alerts, an append-only movement ledger, and expired-stock sale prevention enforced server-side.
- **Safe by construction:** Atomic idempotent checkout, atomic purchase receiving, validated returns that can never exceed what was sold, and an append-only audit log.
- **Offline-resilient:** Sales queue locally during outages and sync by idempotency key — no lost sales, no duplicates, visible conflicts for manual resolution.
- **Calm and fast:** Native-feeling Angular SPA, permission-gated navigation, no unnecessary features in the cashier's face.

---

## Key Features

### 1. POS Checkout
- Barcode/keyboard-first search (brand, generic, barcode, manufacturer)
- Cart with merge rules, quantity guards, line and order discounts
- Atomic idempotent sale completion: sale + items + FEFO inventory deduction + movements + split-tender payment + audit in one transaction
- Sequential invoice numbers, printable receipts (Bangla/English, thermal-friendly)

### 2. Inventory — Batch & Expiry
- Batch-level stock with manufacturing/expiry dates, supplier and purchase reference
- FEFO allocation on every sale; expired batches can never be sold
- Low-stock, near-expiry and expired-stock alerts; stock adjustments with reasons
- Append-only inventory movement ledger (`PURCHASE_RECEIVED`, `SALE`, `SALE_RETURN`, `ADJUSTMENT_IN/OUT`, `EXPIRED` …)

### 3. Purchasing
- Tenant-scoped suppliers with history and balance tracking
- Draft → received/cancelled purchase workflow with batch capture
- Atomic idempotent receiving straight into batch stock

### 4. Customers & Returns
- Optional quick customer creation (no forced accounts for cash sales), search, purchase history
- Sale lookup, per-item return quantities, reason capture, restock + refund recording
- Over-return impossible: returns can never exceed originally sold quantities

### 5. Dashboard & Reports
- Tenant-scoped dashboard: today's sales, transactions, revenue, low-stock, near-expiry, top medicines, recent sales
- Sales (daily/weekly/monthly, by cashier, method, medicine), inventory (stock, low, expiry, movement, valuation) and purchase reports
- Date filters, search, sorting, pagination, CSV/PDF export

### 6. Offline-Safe Sales
- Connectivity heartbeat against the anonymous platform endpoint; honest offline banner
- Checkout intents persist in a durable FIFO queue (capped, tenant-scoped) and replay by idempotency key — fresh and replayed sales are treated identically
- 422 conflicts surface for manual resolution; nothing is silently overwritten or dropped

### Cross-cutting
- Bangla/English toggle everywhere, Bengali digits, Bangla receipts
- Role-based permissions (Owner, Manager, Cashier, Inventory Staff) resolved server-side per request
- Append-only audit log for logins, price changes, stock moves, sales, returns and config changes

---

## Tech Stack

**Backend:**
- .NET 10, ASP.NET Core Web API, Clean Architecture, MediatR 12.5 (CQRS), FluentValidation 12.1.1
- Entity Framework Core 10 + Npgsql, PostgreSQL 17
- Finbuckle.MultiTenant 10.x (shared DB + `TenantId` discriminator, EF global query filters)
- Keycloak 26 (OIDC, Authorization Code + PKCE S256 SPA, bearer-only API)
- Serilog structured logging, Swashbuckle, HealthChecks (liveness + Postgres readiness)

**Frontend:**
- Angular 22 (zoneless, standalone components), TypeScript, `keycloak-js` 26, RxJS, Vitest

**Infrastructure & Quality:**
- Docker + Compose v2 (dev stack + hardened production stack with TLS reverse proxy)
- xUnit, FluentAssertions, Testcontainers.PostgreSql + Respawn (87 unit + 51 integration), 11 frontend unit tests
- GitHub Actions CI: backend build + test, frontend build + test, image builds, production validation (compose config, EF migration bundles, proxy template, prod-default guards)

See `Directory.Packages.props`, `frontend/pharmacy-pos/package.json` and `.github/workflows/` for full versions.

---

## Architecture Overview

Thin controllers → MediatR use cases → tenant-filtered EF Core → one shared PostgreSQL database:

- `PharmaPOS.Domain` — entities, value objects, domain errors; no dependencies
- `PharmaPOS.Application` — CQRS handlers, validators, pipeline behaviors (validation, logging)
- `PharmaPOS.Infrastructure` — EF Core, Finbuckle store/strategy, repositories, audit writer, migrations
- `PharmaPOS.Api` — composition root: JWT validation, permission policies, middleware (correlation id, security headers, RFC 7807 errors), health checks, rate limiting
- `frontend/pharmacy-pos` — `core/` (auth, HTTP envelope handling, i18n, offline queue) + `features/` (pos, inventory, purchasing, reports, admin)

Tenancy flow: `Keycloak → Authenticated User → AppUser / TenantMembership → Finbuckle.MultiTenant → Current Tenant Context → EF Core tenant-aware filtering → PostgreSQL`. The backend never trusts a client-supplied tenant id, price, total, stock value or role.

---

## Getting Started

### Prerequisites

- [.NET SDK **10.0**](https://dotnet.microsoft.com/download)
- [Node.js **24+**](https://nodejs.org) and npm **11+** (only for running the SPA outside Docker)
- [Docker](https://www.docker.com) with Compose v2 (the simplest way to run everything)
- Optional: `dotnet-ef` (`dotnet tool install --global dotnet-ef`)

### Run Locally

1. Clone the repo:
   ```bash
   git clone https://github.com/InfinityAbir/pharmapos.git
   ```
2. Configure local secrets and start the whole stack:
   ```powershell
   Copy-Item .env.example .env      # then edit POSTGRES_PASSWORD / KEYCLOAK_ADMIN_PASSWORD
   docker compose up --build
   ```
3. Wait for Keycloak to report **healthy** (first start imports the realm, ~1–2 minutes), then open http://localhost:4200 and sign in with a demo identity below.

| Service | URL |
| --- | --- |
| Web SPA | http://localhost:4200 |
| API | http://localhost:8080 |
| API Swagger (Development) | http://localhost:8080/swagger |
| API health | http://localhost:8080/health · http://localhost:8080/health/ready |
| Keycloak admin console | http://localhost:8090 (realm `pharmapos`) |
| PostgreSQL | `localhost:5432` |

### Demo Identities

Imported by Keycloak from `deploy/keycloak/realm-export.json`. Both belong to tenant `demo-pharmacy` but carry different permissions, so you can see the authorization boundary in action.

| User | Password | Sees |
| --- | --- | --- |
| `admin@pharmapos.local` | `Platform#2026!` | `platform.tenants.manage`, `platform.users.manage`, `platform.audit.read` |
| `owner@demo-pharmacy.local` | `Pharmacy#2026!` | `platform.audit.read` |

> These accounts exist for local development only and must never be imported into a real environment.

### Build and Test

```powershell
dotnet build PharmaPOS.slnx -c Release
dotnet test  PharmaPOS.slnx -c Release

cd frontend/pharmacy-pos
npm install
npm run build
npm test -- --watch=false
```

The integration tests start an ephemeral PostgreSQL container, so Docker must be running.

---

## Configuration

- **Environment:** Copy `.env.example` → `.env` for local dev; `.env.production.example` → `.env.production` for production (`docker compose -f docker-compose.prod.yml --env-file .env.production up -d`). Never commit filled files.
- **Keycloak:** Realm, clients (`pharmapos-api` bearer-only, `pharmapos-web` public + PKCE), `tenant_id`/`permissions` protocol mappers and demo users live in `deploy/keycloak/realm-export.json` (dev only). Production builds its realm once per `docs/production.md` — the dev export is never imported there.
- **CORS:** Explicit origin allow-list (`Cors__AllowedOrigins`), no wildcards. The SPA origin must be listed or calls fail by design.
- **Language:** In-app বাংলা / English toggle, persisted; receipts follow the active language.
- **Migrations:** Two DbContexts (`AppDbContext`, `TenantStoreDbContext`). Dev auto-migrates + seeds on startup; production migrates explicitly via `scripts/migrate-prod.sh` (CI-built EF bundles).
- **Backups:** `scripts/backup-db.sh` (nightly cron), `scripts/restore-db.sh` (drill into a scratch DB), `scripts/prod-smoke.sh` (post-deploy checks).

---

## Security

| Control | How |
|---|---|
| Authentication | Keycloak OIDC; short-lived access tokens, rotation + reuse detection owned by Keycloak — the API only validates |
| Tenant isolation | Server-resolved from the validated `tenant_id` claim; EF global filters; foreign-`TenantId` writes rejected; covered by `TenantIsolationTests` |
| Authorization | `HasPermission` policies re-evaluated per request from the DB membership graph; token claims never trusted as outcomes |
| Transport | TLS terminated at the production reverse proxy (HSTS, modern ciphers); `RequireHttpsMetadata` enforced |
| Headers | `nosniff`, `DENY` framing, `no-referrer`, locked-down permissions policy, API `default-src 'none'`, SPA CSP at the proxy |
| Abuse resistance | Per-user/IP rate limiting + strict auth-endpoint policy, mirrored at the proxy |
| Error handling | RFC 7807 problem responses with stable codes + `traceId`; 500s never leak internals |
| Secrets | Host env only (`.env.production`, mode 600); never baked into images, never logged, never committed |

---

## Project Structure

```
pharmapos/
├── src/
│   ├── PharmaPOS.Domain/          # Entities, value objects, domain errors — no dependencies
│   ├── PharmaPOS.Application/     # Use cases (MediatR), validation, abstractions
│   ├── PharmaPOS.Infrastructure/  # EF Core, multi-tenant store, repositories, migrations
│   └── PharmaPOS.Api/             # Composition root, auth pipeline, controllers, middleware
├── frontend/
│   └── pharmacy-pos/              # Angular SPA (core/ + features/, runtime config.json)
├── tests/
│   ├── PharmaPOS.UnitTests/
│   └── PharmaPOS.IntegrationTests/   # Testcontainers PostgreSQL + Respawn
├── deploy/
│   ├── keycloak/                  # Realm import (dev) + branded login theme
│   ├── proxy/                     # Production nginx template + certs (git-ignored)
│   └── web/                       # Production SPA config template
├── scripts/                       # backup-db, restore-db, migrate-prod, prod-smoke
├── docs/                          # architecture, development, production, checklist, ADRs, phase plans
├── .github/workflows/             # CI: backend, frontend, containers, production
├── docker-compose.yml             # postgres + keycloak + api + web (development)
├── docker-compose.prod.yml        # hardened production stack (standalone)
├── .env.example / .env.production.example
├── PharmaPOS.slnx
└── README.md
```

---

## Roadmap

- [ ] Multi-branch support (branch-scoped inventory, transfers, branch reports)
- [ ] Subscription plans and billing (trial/starter/business tiers)
- [ ] Reporting replica + horizontal API scaling for larger fleets
- [ ] Barcode label printing and weighing-scale integration
- [ ] Prescription document attach + reference linking (business data only, no clinical features)
- [ ] Cloud sync status dashboard and push notifications (low-stock, expiry)
- [ ] Grocery/clothing variant modules on the shared SaaS core

Contributions and issue reports are welcome.

---

## Contributing

1. Fork the repo
2. Create a feature branch: `git checkout -b feature/your-feature`
3. Follow the phase discipline: understand → design → implement → test → review; keep controllers thin, business rules in domain/application, every tenant-owned query tenant-scoped
4. Add/extend automated tests for business rules (unit) and critical flows (integration); keep `TenantIsolationTests` green
5. Commit: `git commit -m "Add your feature"`
6. Push and open a Pull Request

Please keep Bangla strings intact (UTF-8), never trust client-supplied tenant ids, prices or permissions, and include screenshots for UI changes.

---

## License

This project is currently **All Rights Reserved** — source available for learning and review.

If you want to reuse code or contribute a licensed fork, please contact the author. A formal open-source license (e.g., MIT) can be added on request.

---

## Author & Contact

**Abir Hasan (InfinityAbir)** — ASP.NET Core / Android developer, building clean, secure, real-world apps.

- GitHub: https://github.com/InfinityAbir
- Portfolio: https://infinityabir.github.io/abir-hasan-portfolio/
- LinkedIn: https://www.linkedin.com/in/infinityabirhasan/
- Email: abirha3896@gmail.com

> If PharmaPOS helps your pharmacy run better, please ⭐ star the repo and share feedback via Issues.
