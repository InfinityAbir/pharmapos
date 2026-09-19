# Development guide

How to work on PharmaPOS locally: run it, change it, migrate it, and fix the things that commonly
go wrong.

## 1. Prerequisites

- .NET SDK **10.0** (`dotnet --version` → `10.x`)
- Node.js **24+**, npm **11+**
- Docker with Compose v2
- Optional but handy: `dotnet tool install --global dotnet-ef`

## 2. First run

```powershell
Copy-Item .env.example .env
# Edit .env: set POSTGRES_PASSWORD (and optionally KEYCLOAK_ADMIN_PASSWORD).
docker compose up --build
```

Wait for `pharmapos-keycloak-1` to report **healthy** (first start imports the realm and takes
~1–2 minutes). Then open http://localhost:4200 and sign in with a demo identity from the
[README](README.md#demo-identities).

The API applies EF Core migrations and seeds the demo tenant on startup because
`Database__ApplyMigrationsOnStartup` and `Database__SeedDemoData` are `true` in `.env` /
`appsettings.Development.json`. Both are development-only switches; leave them off in production.

### Running pieces individually

Start just the dependencies, then run the API and SPA from source:

```powershell
docker compose up -d postgres keycloak
dotnet run --project src/PharmaPOS.Api          # http://localhost:8080
cd frontend/pharmacy-pos; npm start             # http://localhost:4200
```

The SPA reads its runtime configuration from `public/config.json` (served as `/config.json`), so a
single build can be pointed at a different API or Keycloak without rebuilding. No dev proxy is
used; the API allows the SPA origin through `Cors__AllowedOrigins__0`.

## 3. Tests

```powershell
dotnet build PharmaPOS.slnx -c Release
dotnet test  PharmaPOS.slnx -c Release

cd frontend/pharmacy-pos
npm test -- --watch=false
npm run build
```

The integration tests spin up a throwaway `postgres:17-alpine` container (Testcontainers) and reset
state between tests with Respawn, so **Docker must be running**. They cover tenant isolation,
`TenantId` stamping, rejection of foreign `TenantId` writes and tenant-store round-trips.

## 4. Database migrations

There are **two DbContexts**, both in `PharmaPOS.Infrastructure`:

| Context | Purpose | Migrations folder |
| --- | --- | --- |
| `AppDbContext` | Tenant-scoped application data | `Persistence/Migrations` |
| `TenantStoreDbContext` | The tenant directory (Finbuckle store) | `Identity/Tenancy/Migrations` |

Add a migration from the repository root:

```powershell
dotnet ef migrations add <Name> `
  --project src/PharmaPOS.Infrastructure `
  --startup-project src/PharmaPOS.Api `
  --context AppDbContext `
  --output-dir Persistence/Migrations
```

Swap `--context TenantStoreDbContext --output-dir Identity/Tenancy/Migrations` for the tenant
store. Design-time factories build the model without a live tenant and read the connection string
from `ConnectionStrings__Database` (falling back to a local default).

After adding a tenant-scoped entity, remember to:

1. Call `.IsMultiTenant()` in its `IEntityTypeConfiguration`, and
2. Give it a `TenantId` property, so the global query filter and write guard apply.

### Reset the database

```powershell
docker compose down
docker volume rm pharmapos_pgdata   # check `docker volume ls` for the exact name
docker compose up -d
```

## 5. Keycloak realm changes

The realm is defined in `deploy/keycloak/realm-export.json` and imported **only on first start**
(when the database is empty). Editing the file has no effect on an existing realm. To apply
changes, recreate Keycloak with a fresh database:

```powershell
docker compose down
docker volume rm pharmapos_pgdata
docker compose up -d
```

Realm-level notes:

- Token issuance, refresh rotation and reuse detection are managed by Keycloak. Do not add
  application-side token refresh or rotation logic.
- Demo users carry a `tenant_id` attribute and a `permissions` attribute; both are mapped into the
  access token by protocol mappers.
- Passwords must satisfy the realm policy: at least 12 characters with upper, lower, digit and no
  username substring.

## 6. Troubleshooting

**Endless redirect/login loop, or the API returns 401 for a valid session.**
Almost always an issuer mismatch. In Docker, the API fetches OIDC metadata from the internal
`Keycloak__Authority` (`http://keycloak:8080/...`) while browsers receive tokens whose issuer is the
public URL (`http://localhost:8090/...`). Set `Keycloak__ValidIssuers__0` /
`KEYCLOAK_PUBLIC_ISSUER` to the public issuer. See
[ADR-0011](adr/0011-split-horizon-token-validation.md).

**No API logs.**
Logs are structured Serilog output on stdout, configured via the `Serilog` section
(`WriteTo: Console`). Read them with `docker compose logs -f api`. Authentication failures are
logged under the `Security.Authentication` source context with a correlation id.

**`docker compose` fails on a missing variable.**
`POSTGRES_PASSWORD` and `KEYCLOAK_ADMIN_PASSWORD` are required — copy `.env.example` to `.env` and
fill them in.

**Integration tests hang or fail to connect.**
Docker is not running, or the daemon cannot pull `postgres:17-alpine`.

**Frontend calls fail with CORS errors.**
The SPA origin is not in `Cors__AllowedOrigins` / `WEB_ORIGIN`. Add it explicitly; there is no
wildcard fallback by design.

## 7. Conventions

- Layer dependencies point inwards only: `Domain ← Application ← Infrastructure`, with
  `PharmaPOS.Api` as the composition root. Domain has no external dependencies.
- Use cases live in the application layer as MediatR requests; controllers only translate HTTP.
- Authorization is server-side. Add endpoints gated with `[HasPermission(...)]` and register the
  permission in `Permissions`; the SPA mirrors permissions for UX only, never for enforcement.
- Package and framework versions are centralised in `Directory.Packages.props`; add new packages
  there, not in individual `.csproj` files.
