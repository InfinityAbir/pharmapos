# ADR-0011: Split-horizon issuer validation for Keycloak tokens

- **Status:** Accepted
- **Date:** 2026-09-20
- **Phase:** 0 (setup)

## Context

In `docker-compose` the API and Keycloak are containers on the same network. The API must reach
Keycloak's OIDC discovery document and JWKS over the internal service address
(`http://keycloak:8080/realms/pharmapos`), because the public address (`http://localhost:8090`)
resolves to the developer's machine, not to the API container.

Keycloak derives the `iss` claim it puts in tokens from the address the **browser** used. In local
development that is `http://localhost:8090/realms/pharmapos`. The discovery document fetched over
the internal address reports an issuer of `http://keycloak:8080/realms/pharmapos`.

Because JwtBearer validates `iss` against the discovery document by default, every otherwise-valid
token was rejected with `401`, which the SPA surfaced as an endless login loop.

## Decision

- The API keeps using the **internal authority for discovery/JWKS** (`Keycloak:Authority`).
- The issuers actually accepted at validation time are configured explicitly via
  `Keycloak:ValidIssuers` (comma/indexable list; `Keycloak__ValidIssuers__0` in compose), and
  override the discovery document's issuer.
- `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime` and `ValidateIssuerSigningKey` remain
  **enabled**. The audience (`pharmapos-api`) is asserted independently, so accepting the public
  issuer does not weaken audience checks.

## Consequences

- The same image works behind any internal/public address pair; only configuration differs.
- Deployments must set `ValidIssuers` to the issuer(s) that appear in tokens, and must not set it
  to a wildcard or to a value the identity provider never emits.
- Rejected tokens are logged by the `Security.Authentication` logger with a correlation id, so a
  misconfiguration is observable rather than silent.
