# ADR-0001: Clean Architecture and layering

- **Status:** Accepted
- **Date:** 2026-09-20
- **Phase:** 0

## Context

The bootstrap prompt requires Clean Architecture, SOLID and DI, while warning against adding layers
or abstractions for their own sake.

## Decision

Adopt four projects with one-directional dependencies:

```text
Domain  ◀── Application  ◀── Infrastructure
                  ▲                 ▲
                  └──── API ────────┘
```

- **Domain** — entities, value objects, domain events, invariants. No framework dependencies.
- **Application** — use cases (MediatR), validators, DTOs, and the interfaces Infrastructure
  implements. Depends only on Domain and abstraction packages (MediatR, FluentValidation).
- **Infrastructure** — EF Core/PostgreSQL, tenant store, Keycloak/identity integration, and any
  outbound service. Implements Application interfaces.
- **Api** — composition root, HTTP endpoints, middleware. Controllers/endpoints stay thin; no
  business rules.

## Consequences

- Business rules are testable without a host or database.
- The composition root is the only place that knows every layer.
- A `SharedKernel` project is deliberately not created until a concrete type justifies it.
