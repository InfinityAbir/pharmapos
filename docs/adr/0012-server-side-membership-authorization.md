# ADR-0012: Server-side authorization via the membership graph

- **Status:** Accepted
- **Date:** 2026-09-20
- **Phase:** 1 (identity & tenancy)

## Context

Phase 0 authorized from the Keycloak-issued `permissions` token claim. That was expedient but has
two structural weaknesses for a multi-tenant SaaS:

1. Token claims are minted at login and go stale the moment an administrator changes a member's
   roles — revocation only takes effect at the next login.
2. The set of permissions a caller holds *in the active tenant* is application state, but it lived
   in the identity provider, outside the tenant-filtered database and outside the audit trail.

Phase 1 introduces the membership model (`ApplicationUser → TenantMembership → Role →
Permission`, see ADR-0003) and moves the authorization decision into the database.

## Decision

- The access token proves **who** the caller is (`sub` / `preferred_username`) and **which tenant**
  is active (`tenant_id`, consumed by Finbuckle before authorization runs). Nothing else in the
  token authorizes anything.
- **What** the caller may do is resolved server-side on every request by
  `IMembershipResolver` against the current tenant's membership graph. Callers without an active
  membership resolve to `null` and are denied.
- `PermissionAuthorizationHandler` is therefore **scoped** (it needs the request-scoped
  tenant-filtered `AppDbContext`), and `GET /platform/whoami` returns the resolved
  `CurrentMembershipDto` (user, tenant, membership status, roles, permissions); unknown members
  receive `403 membership.not_found`.
- Roles, memberships and their join rows are tenant-scoped with **tenant-composite foreign keys**
  (`(RoleId, TenantId)`, `(MembershipId, TenantId)`), so a cross-tenant role assignment is
  structurally impossible, not merely rejected by application code.
- The legacy `permissions` token claim is **ignored** by the API. E2E verified this explicitly: a
  realm user carrying `permissions = [platform.tenants.manage]` but no membership row still gets
  `403` on introspection and sees no gated navigation.
- System roles (`IsSystem`) are seeded and immutable (cannot be renamed or deleted).
- Frontend gating (`SessionService.hasPermission`, `permissionGuard`, permission-conditioned nav)
  is UX only; every endpoint re-authorizes via `[HasPermission]`.

## Consequences

- Permission changes take effect on the next request — no re-login required.
- Anonymous shape of `whoami` changed (v2 `CurrentMembershipDto`); the SPA session layer consumes
  it via `SessionService`.
- `TenantInfo.Name` is init-only in Finbuckle v10, so platform tenant updates go through
  `ExecuteUpdateAsync` rather than tracked-entity mutation (`PlatformTenantStore`).
- Entities behind tenant-composite alternate keys must be assigned `TenantId` explicitly at `Add`
  time (EF rejects null alternate keys on track; Finbuckle only stamps at save) — centralized in
  `TenantScope.CurrentTenantId`. Related: Clear()+Add on one tracked collection emits UPDATE and
  DELETE for the same rows under composite FKs, so role/permission swaps are delete-then-insert
  across separate `SaveChanges` calls (found by E2E, covered by integration tests).

## Deferred (explicit)

- Provisioning brand-new Keycloak users (invitation flow with temp credentials/email) — needs an
  identity-directory service; Team manages memberships for identities that already exist.
- Multi-membership tenant switching UX — the claim tenant selects the context; listing all member
  tenants needs a non-filtered query surface.
