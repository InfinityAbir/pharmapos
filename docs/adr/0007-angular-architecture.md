# ADR-0007: Angular frontend architecture

- **Status:** Accepted
- **Date:** 2026-09-20
- **Phase:** 0

## Context

The UI must be fast for checkout, keyboard/barcode friendly, support Bangla and English, and be
maintainable. The prompt fixes Angular + TypeScript.

## Decision

- **Angular 22**, standalone components, strict TypeScript, SCSS, route-level lazy loading.
- **Structure**
  - `core/` — singletons: config, auth, HTTP interceptors, error/loading services, app shell.
  - `shared/` — reusable presentational components, directives, pipes.
  - `features/<feature>/` — routed features (dashboard, catalog, inventory, purchasing, pos, ...).
- **State** — Angular signals with thin, feature-local stores. NgRx is deferred until a concrete
  need appears.
- **Auth** — `angular-auth-oidc-client` configured for Keycloak (Authorization Code + PKCE);
  functional `authGuard`; token attached by a functional HTTP interceptor.
- **API layer** — typed `ApiClient` over `HttpClient`; base URL from environment files; error
  interceptor maps RFC 7807 `ProblemDetails` to a consistent shape; correlation id header.
- **UX foundation** — global loading indicator, notification/toast service, consistent empty/error
  states, accessible controls.
- **Checkout (Phase 5)** — a purpose-built, non-CRUD screen optimised for speed, barcode scanning
  and keyboard navigation.

## Consequences

- Environment-specific API/auth configuration needs no code changes.
- Feature teams can evolve independently.
- The checkout UX is treated as a distinct engineering problem, not a form.
