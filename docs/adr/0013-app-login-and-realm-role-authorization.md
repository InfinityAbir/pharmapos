# ADR-0013: App-owned login/registration and realm-role authorization

- **Status:** Accepted
- **Date:** 2026-09-21
- **Phase:** Keycloak alignment (SylviaNG pattern)

## Context

The SPA authenticated via Keycloak-hosted login (Authorization Code + PKCE through
`pharmacos-web`) and the API authorized from the database membership graph (ADR-0012). The
product asks for the recruitment system's pattern instead: the app's own login/registration
pages backed by Keycloak, with authorization from Keycloak realm roles.

## Decision

- **App-owned login/registration.** `POST /api/v1/auth/login|refresh|register` proxy Keycloak
  server-side (Resource Owner grant + Admin REST) using the confidential `pharmacos-api`
  client. The SPA stores the returned tokens and refreshes through the API. No OIDC redirects,
  no `keycloak-js`. `pharmacos-web` stays in the dev realm export, unused by the SPA.
- **Realm roles authorize.** `realm_access`/`roles` claims map to role claims at authentication;
  `PermissionAuthorizationHandler` grants a permission when any of the caller's realm roles holds
  it per `RealmRoles.RolePermissions` (grants copied from the seeded system roles, so nobody's
  access widens or narrows in the switch). All 75 `[HasPermission]` call sites work unchanged.
- **Tenancy is untouched.** `tenant_id` claim → Finbuckle → EF filters, exactly as before.
  Registration stamps `tenant_id` on the Keycloak user and provisions the tenant row, owner
  membership and Owner role, so Team/whoami keep working off the membership graph.
- **Refresh tokens** live in SPA local storage; rotation/reuse detection stays Keycloak's.

## Consequences

- Passwords transit the API (HTTPS-only in production, never logged); ROPC is deprecated OAuth
  kept deliberately for the password-form UX — PKCE redirect remains the standards-first option
  if this is ever revisited.
- Role changes take effect at next login (token-minted), not next request — the inverse of the
  ADR-0012 tradeoff, accepted for operational simplicity (one role system, managed in Keycloak).
- Registration is an open endpoint: strict rate limiting (`authentication` policy), strong
  password rules mirroring the realm policy, and Keycloak-first ordering with Admin-API
  compensation keep it abuse-resistant and orphan-free.
- ADR-0012's membership-graph authorization is superseded; the membership *data* (Team page,
  whoami context) is retained. ADR-0004's "bearer-only API / PKCE SPA" client split is
  superseded for the login path.

## Deferred (explicit)

- Forgot-password / OTP / invite-email flows (SylviaNG has them; POS does not need them yet).
- Removing the vestigial DB permission graph once Team/Roles pages are re-scoped.
