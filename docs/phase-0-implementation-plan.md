# Phase 0 — Foundation: implementation plan

> Scope, dependencies and decisions for Phase 0, per the bootstrap prompt.
> Phase 0 delivers a **runnable, tested skeleton**. It contains **no pharmacy business modules**.

## Scope

### Backend
- ASP.NET Core Web API host (`PharmaPOS.Api`) on .NET 10 (LTS).
- Clean Architecture projects: Domain, Application, Infrastructure, Api.
- Dependency injection + strongly-typed, validated configuration.
- PostgreSQL connection; EF Core + migrations toolchain.
- Global exception handling → RFC 7807 `ProblemDetails`.
- FluentValidation pipeline behavior.
- MediatR pipeline (logging + validation) where useful.
- Structured logging (Serilog) with correlation id.
- Swagger/OpenAPI.
- Health checks (liveness + readiness incl. PostgreSQL).
- Test projects (unit, integration, E2E).

### Frontend
- Angular 22 application (`frontend/pharmacy-pos`).
- Routing + lazy feature loading.
- Application shell (layout, navigation).
- Authentication integration foundation (Keycloak OIDC/PKCE wiring).
- API communication layer (typed client, interceptors).
- Environment configuration.
- Loading/error foundation (global indicator + notifications + error mapping).

### Infrastructure
- Docker Compose: PostgreSQL, Keycloak, backend, frontend.
- Keycloak realm import (`pharmapos` realm, web + api clients).
- Dockerfiles for API and frontend (nginx).
- `.env.example` and scripts.
- Documented local run procedure.

### CI
- GitHub Actions: backend restore/build/test, frontend install/build/test, docker build.

## Explicitly out of scope for Phase 0
- Tenant/user/membership entities, Finbuckle EF filtering (Phase 1).
- Any medicine/inventory/purchasing/POS/returns/reporting feature.
- Subscription/billing and offline sync.

## Dependencies
- .NET 10 SDK — present.
- Node 24 / npm 11 — present.
- Docker Desktop (Linux engine) — required for compose and integration tests.
- Keycloak, PostgreSQL — provided as containers.

## Key decisions (see ADRs)
- ADR-0001 Clean Architecture layers.
- ADR-0002 shared DB + `TenantId`.
- ADR-0003 tenant resolution & membership.
- ADR-0004 Keycloak.
- ADR-0005 MediatR + FluentValidation.
- ADR-0006 transactions/concurrency/idempotency.
- ADR-0007 Angular.
- ADR-0008 testing.
- ADR-0009 dependency/license policy.

## Definition of Done for Phase 0
- `dotnet build` succeeds; `dotnet test` (unit) passes.
- Integration tests pass against real PostgreSQL (Testcontainers).
- `docker compose up` starts PostgreSQL, Keycloak, API and web; `/health` and `/health/ready`
  report healthy.
- Swagger/OpenAPI reachable.
- Angular app loads, shows the shell, and successfully calls the API info endpoint through the
  typed API layer (proves routing + HTTP + error/loading foundation).
- Keycloak realm is importable and the frontend is configured against it.
- CI workflow is committed.
- README + docs explain how to run the full environment locally.
- E2E (Playwright) smoke check passes against the running compose stack.
- Phase committed and pushed to the private `pharmapos` GitHub repo.
