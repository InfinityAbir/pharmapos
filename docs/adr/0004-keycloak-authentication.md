# ADR-0004: Authentication with Keycloak (OIDC / JWT)

- **Status:** Accepted
- **Date:** 2026-09-20
- **Phase:** 0 (setup) / 1 (integration)

## Context

PRD §6 recommends Keycloak and requires that tenant context be derived from authenticated identity.
Credentials must be handled by an identity provider rather than the application.

## Decision

- **Keycloak is the identity provider.** One realm per environment (`pharmapos`), configured by an
  importable realm JSON.
- **Clients**
  - `pharmapos-web` — public SPA client, Authorization Code + PKCE, no secret.
  - `pharmapos-api` — bearer-only resource server.
- **The API validates JWTs** against the realm's OIDC metadata/JWKS and enforces issuer, audience,
  lifetime and signature via ASP.NET Core JwtBearer.
- **Identity is linked, not duplicated.** `ApplicationUser.KeycloakSubject` maps the token `sub` to
  a local application user; Keycloak remains the source of credentials.
- **Authorization is application-owned.** Realm roles are limited to platform concerns; business
  roles/permissions are stored in the application database per tenant and evaluated server-side.
- Tenant/user administration uses the Keycloak Admin API from a server-side service only.

## Consequences

- No passwords or password hashes ever live in the application database.
- The application can enforce authorization independent of token contents.
- Local development depends on the Keycloak container (docker-compose).
