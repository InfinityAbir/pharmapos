# Phase 1 — Identity & Tenancy: implementation plan

> Scope and design for Phase 1, per the PRD roadmap (§49 "Identity & Tenancy": Tenant model,
> Authentication, Roles, Permissions, Tenant resolution) and ADR-0003 / architecture §7.

## Motivation

Phase 0 proved tenancy with audit logs only and authorized from the Keycloak `permissions` claim.
Phase 1 introduces the **membership model** and makes the **database the authority for
authorization**: the token still proves *who* the caller is and *which tenant* is active; what the
caller *may do* is resolved server-side from `ApplicationUser → TenantMembership → Role →
Permission`.

## Scope

### Backend
- Domain model (all in `PharmaPOS.Domain.Identity`):
  - `ApplicationUser` — platform-level, `KeycloakSubject` (unique), `Email` (unique, stored lowercase),
    `DisplayName`, `IsActive`.
  - `TenantMembership` — tenant-scoped, `UNIQUE (TenantId, ApplicationUserId)`, status + default flag.
  - `Role` — tenant-scoped, `UNIQUE (TenantId, Name)`, `IsSystem`.
  - `Permission` — platform catalog, `UNIQUE (Code)`.
  - `RolePermission` / `MembershipRole` — tenant-scoped joins with tenant-composite FKs so
    cross-tenant references are structurally impossible.
- Permission catalog extended with `tenant.members.manage`, `tenant.roles.manage`.
- **`IMembershipResolver`** (Application abstraction, Infrastructure implementation): resolves the
  current tenant membership for an identity and returns its tenant roles + merged permissions.
- **Authorization moves to the database**: `PermissionAuthorizationHandler` consults the resolver
  instead of the `permissions` claim. Users without an active membership are denied (403 on
  introspection).
- `GET /platform/whoami` returns the server-resolved context: user, tenant, membership status,
  roles, permissions.
- Tenant administration (platform, `platform.tenants.manage`): list, create, set status.
- Role administration (tenant, `tenant.roles.manage`): list, create, update, delete (system roles
  immutable).
- Membership administration (tenant, `tenant.members.manage`): list members, set status, replace
  roles.
- Audit events on every mutation (`tenant.created`, `role.created`, `membership.roles.updated`, …).
- Migration `TenancyInitialCreate` + idempotent seed (permission catalog, demo users, memberships,
  V1 tenant roles).

### Frontend
- `SessionService` holds the server-resolved context and exposes `hasPermission(...)` for gating.
- Navigation gated by permission; tenant pages guarded.
- Pages: **Tenants** (platform), **Team**, **Roles**.
- Dashboard shows server-resolved roles.

## Explicitly out of scope (deferred, documented)
- Provisioning brand-new Keycloak users (full "invitation": create realm user, temp credentials,
  email). Team membership is managed for identities that already exist in the realm; creating
  identities requires an identity-directory service (later phase).
- Tenant switching UX for multi-membership users (resolved against the claim tenant; listing all
  member tenants needs a non-filtered context, deferred).
- Pharmacy profile (Phase 2).

## Decisions / ADRs
- New ADR-0012: server-side authorization via the membership graph.

## Definition of Done
- `dotnet build` clean; unit + integration suites green (isolation, membership resolution,
  cross-tenant role rejection).
- Stack runs; E2E: admin sees Tenants/Team/Roles and creates a tenant + role; owner (no
  `platform.tenants.manage`) sees Team/Roles but not Tenants; a user with no membership gets 403.
- Frontend build + tests green.
- Docs updated and phase committed to the private `pharmapos` repo.