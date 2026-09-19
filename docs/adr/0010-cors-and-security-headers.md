# ADR-0010: Browser-facing CORS, security headers and rate limiting

- **Status:** Accepted
- **Date:** 2026-09-20
- **Phase:** 0 (setup)

## Context

The Angular SPA runs on a different origin from the API (separate ports/hosts in every
environment). A browser will therefore enforce CORS, and the API is the only place where the
platform can set response hardening headers at all. Being a multi-tenant system holding pharmacy
data, the browser boundary is treated as hostile by default.

## Decision

- **CORS uses an explicit origin allow-list**, bound from `Cors:AllowedOrigins`. When the list is
  empty the policy is not registered at all, so an environment cannot accidentally fall back to a
  permissive default.
  - Allowed methods are limited to `GET/POST/PUT/PATCH/DELETE`.
  - Allowed request headers are limited to `Authorization`, `Content-Type` and `X-Correlation-Id`.
  - Only `X-Correlation-Id` is exposed to script.
  - `AllowAnyOrigin` is never used; credentials are allowed only with named origins.
- **Security headers are applied by `SecurityHeadersMiddleware` on every response**, independent of
  the endpoint (including error and 401/403 responses).
- **Rate limiting is enforced server-side**: a global per-caller fixed window, plus a stricter
  `authentication` policy for credential-adjacent endpoints, rejected with `429` and an RFC 7807
  body.
- CORS is configured from `Cors:AllowedOrigins` and never from request data.

## Consequences

- Adding a new front-end origin is an explicit configuration change per environment.
- The API can be consumed from a browser only by allow-listed origins, which limits the blast
  radius of token theft from a compromised origin.
- Security headers are guaranteed even on short-circuited responses because the middleware runs
  before authentication and authorization.
