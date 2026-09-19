# ADR-0009: Dependency and license policy

- **Status:** Accepted
- **Date:** 2026-09-20
- **Phase:** 0

## Context

Several popular .NET libraries changed to commercial licensing in 2025. This is a commercial SaaS
product, so license cost and legal clarity matter.

## Decision

Prefer permissive, no-fee dependencies. Pinned choices:

| Package | Version | License | Note |
|---|---|---|---|
| MediatR | **12.5.0** | Apache-2.0 | 13+ is commercially licensed |
| FluentValidation | 12.x | Apache-2.0 | |
| FluentAssertions | **7.x** | Apache-2.0 | 8+ is commercially licensed (Xceed) |
| Finbuckle.MultiTenant | 10.x | Apache-2.0 | |
| Serilog.AspNetCore | 10.x | Apache-2.0 | |
| Swashbuckle.AspNetCore | 10.x | MIT | |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.x | PostgreSQL License | |
| Testcontainers | 4.x | MIT | |
| Respawn | 7.x | MIT | |

- Versions are centralised in `Directory.Packages.props`.
- New dependencies require a license check; copyleft or source-available licenses are rejected for
  product code.

## Consequences

- No recurring license fees for the core stack.
- Pinning deliberately trails the newest major of MediatR/FluentAssertions to stay on free terms.
