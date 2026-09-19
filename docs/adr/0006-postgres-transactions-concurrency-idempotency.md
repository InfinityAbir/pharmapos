# ADR-0006: PostgreSQL, transactions, concurrency and idempotency

- **Status:** Accepted
- **Date:** 2026-09-20
- **Phase:** 0 (foundation) / 5 (sale path)

## Context

Critical operations (sale completion, purchase receiving, returns) must be atomic and safe under
concurrency. PRD §16, §31 and §36 require idempotency and prevention of negative inventory, and the
prompt states frontend validation is insufficient.

## Decision

- **PostgreSQL** with EF Core (Npgsql provider).
- **Money** as `numeric(18,4)`; **timestamps** as UTC `timestamptz`.
- **Atomicity** — critical commands run in a single DB transaction (MediatR `TransactionBehavior`).
- **Concurrency**
  - optimistic concurrency using PostgreSQL `xmin` on mutable rows, and
  - **atomic conditional stock updates** (`UPDATE ... SET qty = qty - @n WHERE qty >= @n`) so the
    database, not the application, is the arbiter. A zero-row update means insufficient stock.
- **Idempotency** — critical commands carry a client-generated operation id; a tenant-scoped,
  unique idempotency record stores the request hash and the produced result. Replays return the
  stored outcome instead of re-executing.
- **Integrity** — foreign keys (tenant-inclusive), unique constraints, tenant-leading indexes,
  `CHECK` constraints where meaningful.

## Consequences

- Concurrent cashiers cannot oversell or create negative stock, even with stale client data.
- Duplicate submissions (double-click, retry, refresh) are safe.
- Handlers that mutate stock must use the provided stock APIs rather than blind entity updates.
