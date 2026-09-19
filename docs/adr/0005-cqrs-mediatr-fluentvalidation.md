# ADR-0005: CQRS with MediatR and FluentValidation pipeline

- **Status:** Accepted
- **Date:** 2026-09-20
- **Phase:** 0

## Context

The prompt requires CQRS/MediatR "where useful", a FluentValidation pipeline, and thin controllers,
while warning against over-abstraction.

## Decision

- Use **MediatR** to model use cases as commands/queries with handlers. Controllers/endpoints only
  send requests and translate results to HTTP.
- Cross-cutting concerns are implemented as **pipeline behaviors**:
  - `LoggingBehavior` — structured request/response logging and timing;
  - `ValidationBehavior` — runs all `IValidator<TRequest>` and throws a validation exception mapped
    to `ValidationProblemDetails`;
  - `TransactionBehavior` (added when the first write module lands) — wraps command handlers that
    opt in.
- **MediatR 12.5.0** is pinned. MediatR 13+ is commercially licensed; 12.5.0 is the last
  Apache-2.0 release and keeps the same API. See ADR-0009.
- Queries use projected DTOs; no generic repository. Data access is via EF Core `DbContext`
  abstractions owned by Application.

## Consequences

- Business rules never live in controllers.
- Adding a new use case is a request + handler + validator + endpoint.
- Avoids a generic repository/factory layer that would add indirection without value.
