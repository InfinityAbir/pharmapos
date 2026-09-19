# ADR-0008: Testing strategy

- **Status:** Accepted
- **Date:** 2026-09-20
- **Phase:** 0

## Context

The prompt requires xUnit unit/integration/API tests and E2E, with tenant-isolation tests proving
Tenant A cannot access Tenant B data, and PRD §43 defines the layers.

## Decision

| Layer | Tooling |
|---|---|
| Unit | xUnit + FluentAssertions 7 |
| Integration | xUnit + `WebApplicationFactory` + **Testcontainers PostgreSQL** + Respawn |
| API | `WebApplicationFactory` (auth/authz/validation/ProblemDetails/pagination) |
| E2E | **Playwright** against the docker-compose stack |

- Integration tests use a **real PostgreSQL** container, not an in-memory provider, so filters,
  constraints, transactions and concurrency are genuinely exercised.
- Each test class gets an isolated, migrated database; Respawn resets data between tests.
- **Tenant isolation is a release gate**: a reusable harness creates two tenants and asserts
  cross-tenant reads/writes fail.
- E2E verification runs before each phase is committed and pushed ("after e2e check").

## Consequences

- Integration tests require Docker; they are tagged so they can be skipped where Docker is absent,
  but CI always runs them.
- No reliance on EF InMemory/SQLite for behaviour that differs from PostgreSQL.
