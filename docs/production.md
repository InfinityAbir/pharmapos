# Production deployment guide

How to deploy, operate, back up and recover PharmaPOS in production. The development
workflow stays in [`development.md`](development.md); production uses **only**
`docker-compose.prod.yml` — never chain it with the dev `docker-compose.yml`.

```text
Internet (443/80)
    │
    ▼
Reverse proxy (nginx) ── TLS termination, HSTS/CSP, rate-limit pre-filter
    ├── /api/*, /health* ──► API (ASP.NET Core, Production)
    ├── / ──► Web (Angular SPA, runtime config.json)
    └── /realms/*, /resources/*, /admin/* ──► Keycloak 26 (start mode, kcdb)
                                                   │
App Postgres ◄── API ──► EF Core migrations (explicit, pre-deploy)
                                            Keycloak Postgres (kcdb, isolated)
```

## 1. Prerequisites

- Linux host (Ubuntu 24.04 LTS or equivalent) with Docker Engine + Compose v2.
- Public DNS: `PUBLIC_HOST` (e.g. `pos.example.com`) pointing at the host.
- TLS certificates for `PUBLIC_HOST` at `deploy/proxy/certs/fullchain.pem` +
  `privkey.pem` (Let's Encrypt via certbot, or your CA — never commit them).
- CI-built images `pharmapos-api` / `pharmapos-web` (pinned per release), or build
  on the host with `docker compose -f docker-compose.prod.yml build`.
- Filled `.env.production` (from [`.env.production.example`](../.env.production.example))
  and `deploy/web/config.prod.json` (from
  [`deploy/web/config.prod.json.example`](../deploy/web/config.prod.json.example)).

## 2. Required environment variables / secrets

| Variable | Secret? | Purpose |
|---|---|---|
| `PUBLIC_HOST` | no | Browser-reachable hostname (CORS origin, TLS SNI, Keycloak hostname base). |
| `KEYCLOAK_PUBLIC_HOST` | no | Defaults to `PUBLIC_HOST`. Dedicated `auth.*` host if Keycloak is split out. |
| `POSTGRES_DB` / `POSTGRES_USER` | no | App database name/role. |
| `POSTGRES_PASSWORD` | **yes** | App DB password (`openssl rand -base64 32`). |
| `KC_DB_NAME` / `KC_DB_USER` | no | Keycloak database name/role (different from app). |
| `KC_DB_PASSWORD` | **yes** | Keycloak DB password, different from `POSTGRES_PASSWORD`. |
| `KEYCLOAK_ADMIN` | no | Bootstrap admin username. |
| `KEYCLOAK_ADMIN_PASSWORD` | **yes** | Bootstrap admin password (min 16 chars); rotate after first login. |
| `KEYCLOAK_REALM` / `KEYCLOAK_AUDIENCE` | no | Realm (`pharmapos`) and API audience (`pharmapos-api`). |
| `KEYCLOAK_HOSTNAME` | no | Public Keycloak URL, e.g. `https://pos.example.com`. |
| `KEYCLOAK_PUBLIC_ISSUER` | no | Issuer the API accepts, e.g. `https://pos.example.com/realms/pharmapos`. Must be browser-reachable. |
| `API_IMAGE` / `WEB_IMAGE` | no | Pinned image refs per release (digest or immutable tag). |
| `ConnectionStrings__Database` | **yes** | Used by `scripts/migrate-prod.sh` only (EF bundles); same value compose builds. |

Secrets management rules:

- Secrets travel as environment values on the deploy host only (`.env.production`,
  mode `600`, owned by the deploy user). They are never baked into images, never
  committed, never logged (the API never logs tokens or connection strings; auth
  failures log method/path/correlation id only).
- Rotate by updating `.env.production` and recreating the affected service
  (`docker compose -f docker-compose.prod.yml --env-file .env.production up -d <svc>`).
- Prefer a manager (Vault/age/SOPS, cloud secret store) to render `.env.production`
  at deploy time rather than storing it on disk long-term.

## 3. TLS certificates

```bash
# One-time (webroot uses the proxy's ACME challenge location):
docker compose -f docker-compose.prod.yml --env-file .env.production up -d proxy
docker run --rm -v ./deploy/proxy/certs:/etc/letsencrypt \
  -v pharmapos-prod_certbot-www:/var/www/certbot \
  certbot/certbot certonly --webroot -w /var/www/certbot \
  -d pos.example.com --email ops@example.com --agree-tos --non-interactive
cp deploy/proxy/certs/live/pos.example.com/fullchain.pem deploy/proxy/certs/fullchain.pem
cp deploy/proxy/certs/live/pos.example.com/privkey.pem deploy/proxy/certs/privkey.pem
docker compose -f docker-compose.prod.yml --env-file .env.production restart proxy
```

Renew with the same command plus `--force-renewal` on a monthly cron, then restart
`proxy`. The HTTP server block only serves ACME challenges and redirects the rest
to HTTPS.

## 4. Keycloak production configuration

The dev `deploy/keycloak/realm-export.json` (demo users, known passwords) is **never**
mounted in production. Build the realm once, via the admin console (tunnelled over
SSH to the localhost-bound `KEYCLOAK_PORT`):

1. Create realm `pharmapos` (from `KEYCLOAK_REALM`). SSL required: `external`.
2. Keep the realm password policy at least as strict as dev
   (12+ chars, upper/lower/digit, not-username).
3. Clients:
   - `pharmapos-api`: bearer-only resource server. Standard/direct flows OFF,
     service accounts OFF, full-scope OFF.
   - `pharmapos-web`: **public** client, Standard flow ON with PKCE S256,
     valid redirect URIs `https://PUBLIC_HOST/*`, web origins
     `https://PUBLIC_HOST`. No client secret (never ship one to a browser).
4. Protocol mappers on `pharmapos-web` (the API re-validates everything server-side,
   but it needs these claims to resolve identity):
   - `tenant_id` user attribute → access-token claim `tenant_id`.
   - `permissions` user attribute (multivalued) → claim `permissions`.
5. Realm roles `platform-admin` / `pharmacy-owner` (names only; authorization is
   resolved from the application membership graph per ADR-0012, never from raw
   role claims).
6. Users: create real staff accounts with per-user `tenant_id` attributes. Brute-force
   protection stays ON. No shared/demo accounts.
7. Set frontend URL / hostname to `KEYCLOAK_HOSTNAME` so tokens carry the public
   issuer the API allow-lists in `KEYCLOAK_PUBLIC_ISSUER`.

Keycloak runs `start` (production mode) against its own `kcdb` Postgres with
`--proxy-headers=xforwarded`; the proxy overwrites `X-Forwarded-*` (no spoofing).

## 5. Database migrations (production)

Production never migrates on startup (`Database__ApplyMigrationsOnStartup=false`,
`SeedDemoData=false` — both fail-safe in `appsettings.Production.json`). Both
contexts (`TenantStoreDbContext`, then `AppDbContext`) migrate explicitly:

```bash
./scripts/migrate-prod.sh        # bundles from CI artifacts/, else dotnet ef fallback
```

Order per deploy: **backup → migrate → recreate api → smoke test** (see §7).
CI builds both migration bundles on every main-branch run so a broken migration
fails the pipeline, not the deploy.

## 6. First deploy (cold start)

```bash
cp .env.production.example .env.production   # fill EVERY value, chmod 600
cp deploy/web/config.prod.json.example deploy/web/config.prod.json  # real URLs
# place deploy/proxy/certs/{fullchain,privkey}.pem  (see §3)

docker compose -f docker-compose.prod.yml --env-file .env.production up -d postgres kcdb
./scripts/migrate-prod.sh                       # both contexts
# ... Keycloak realm setup per §4 (first start imports nothing) ...
docker compose -f docker-compose.prod.yml --env-file .env.production up -d
./scripts/prod-smoke.sh https://pos.example.com
```

Then create the first tenant + owner via the platform API (authenticated as a
`platform-admin` Keycloak user), and walk
[`production-checklist.md`](production-checklist.md) end to end before opening to users.

## 7. Updates and rollback

```bash
./scripts/backup-db.sh                          # always first
./scripts/migrate-prod.sh                       # pending migrations, if any
API_IMAGE=pharmapos-api:<new> WEB_IMAGE=pharmapos-web:<new> \
  docker compose -f docker-compose.prod.yml --env-file .env.production up -d api web proxy
./scripts/prod-smoke.sh https://pos.example.com
```

Rollback: re-pin the previous image tags, `up -d` again, smoke-test. Migrations are
forward-only; a deploy that needs a destructive migration ships its own backfill
plan reviewed in the PR. Keep the pre-deploy backup until the release is accepted.

## 8. HTTPS, CORS and security headers

- TLS terminates at `proxy` (TLS 1.2+, modern ciphers, HSTS 2 years + subdomains).
  HTTP only redirects (or answers ACME).
- The API behind the proxy learns the real scheme/client IP from overwritten
  `X-Forwarded-For/Proto` (`UseForwardedHeaders`; API port unpublished).
- CORS stays an explicit allow-list: exactly `https://PUBLIC_HOST`, credentials
  allowed, no wildcards (ADR-0010). Extra origins require a config change + redeploy.
- API responses carry `nosniff`, `DENY` framing, `no-referrer`, locked-down
  permissions policy and `default-src 'none'` (JSON only). The proxy adds HSTS, CSP
  (same-origin + Keycloak origin for OIDC), framing denial and referrer policy for
  the SPA/Keycloak pages. Swagger is Development-only and never served in prod.
- Rate limiting is two-layer: nginx 30 r/s pre-filter on `/api/` plus the API's
  per-user/IP fixed window (300/min) and strict auth-endpoint policy (10/min).

## 9. Logging, health checks and monitoring

- Production logs are single-line JSON (Compact) on stdout with correlation ids;
  all services use rotating `json-file` logs (10 MB × 5). Collect stdout centrally
  (journald/loki/ELK) and alert on: `Security.Authentication` warnings spike,
  5xx rate, `HealthCheck` failures, Keycloak login failures.
- Liveness `GET /health` (process up) vs readiness `GET /health/ready` (Postgres
  reachable). The proxy exposes both for load-balancer checks plus its own
  `/healthz`. Compose healthchecks gate startup order (`kcdb → keycloak`,
  `postgres → api → web`, proxy last).
- Uptime checks (external, 1 min): `https://PUBLIC_HOST/health/ready` (2xx),
  `https://PUBLIC_HOST/api/v1/platform/info` (2xx, anonymous by design),
  certificate expiry < 21 days.

## 10. Backup and recovery

- Nightly `scripts/backup-db.sh` (cron 02:00 UTC, `pg_dump` custom format,
  14-day retention, size sanity check). Store `/var/backups/pharmapos` **off** the
  database volume; replicate off-site (rsync/object storage) — a backup on the
  same disk is not a backup.
- Back up `kcdb` the same way (Keycloak realm/keys) by overriding
  `POSTGRES_DB/KC_DB_*` via env when invoking the script, or add a second cron line.
- Quarterly restore drill: `./scripts/restore-db.sh <dump>` restores into a
  **separate** database and verifies tenant/medicine counts plus the migration
  history tail. Promotion to live is manual (maintenance window, §7 order).
- Recovery targets: RPO ≤ 24 h (nightly dumps; tighten cron for busier shops),
  RTO ≤ 2 h (fresh host → compose up → migrate → restore → smoke).

## 11. CI/CD pipeline

`.github/workflows/ci.yml` runs on every push/PR to `main`:

1. `backend`: restore → Release build → unit + integration tests (ephemeral
   Postgres via Testcontainers).
2. `frontend`: `npm ci` → production build → unit tests.
3. `containers`: API + web image builds (same Dockerfiles production uses).
4. `production` (added Phase 10): validates `docker-compose.prod.yml` config,
   builds both EF migration bundles (fails the pipeline on broken migrations),
   and renders the proxy template to catch nginx syntax drift.

Release: tag main (`vX.Y.Z`), CI stamps immutable image tags; deploy pins those
tags in `.env.production` (`API_IMAGE`/`WEB_IMAGE`) and follows §7.

## 12. Production error handling

- All failures surface as RFC 7807/9457 problem responses with a stable `code`
  and `traceId`; unexpected exceptions become a generic 500 with full detail in
  server logs only — internals never leak (see `GlobalExceptionHandler`).
- Domain/business violations return typed 4xx (`validation`, `not_found`,
  `conflict`, `business_rule`) with Bangla/English messages resolved client-side.
- The SPA treats the API as reachable only after the anonymous heartbeat succeeds
  (`ConnectivityService`); checkout intents queue offline and replay idempotently
  by key — a 5xx/retry never double-processes a sale.

## 13. Performance and resources

- Postgres tuned in compose (`shared_buffers` 256 MB, `max_connections` 100,
  slow-query log > 1 s). All list endpoints are paged; dashboard/report queries
  aggregate server-side (Phase 7/9 fixes). Scale vertically first (compose
  `deploy.resources` limits document current budgets: postgres 2C/2G, api 2C/1G,
  keycloak 2C/1.5G).
- Kestrel caps request bodies at 5 MB (matches proxy `client_max_body_size`);
  SPA bundles respect Angular production budgets with hashed immutable assets and
  `no-store` entry HTML/config.
- Capacity rule of thumb: the current single-host sizing fits a pilot fleet
  (tens of pharmacies, hundreds of daily sales each). Beyond that, split reads
  (reporting replica), then per-service hosts — the stateless API image already
  supports horizontal replicas behind the proxy upstream.

## 14. Final security posture (recap)

Backend-authoritative pricing/stock/totals, server-resolved tenancy (never a
client `TenantId`), DB-reevaluated permissions per request, short-lived Keycloak
tokens the API only validates, explicit CORS, security headers + HSTS + CSP,
two-layer rate limiting, secrets via host env only, demo data and Swagger
impossible in production by configuration. Full review trail: Phase 9 hardening
plus the go-live checklist next.
