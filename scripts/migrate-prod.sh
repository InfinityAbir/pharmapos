#!/usr/bin/env bash
# PharmaPOS production migrations — explicit, ordered, auditable.
#
# Production NEVER auto-migrates on container startup
# (Database__ApplyMigrationsOnStartup=false in docker-compose.prod.yml).
# Run this script BEFORE (re)starting the api service on every deploy:
#
#   ./scripts/migrate-prod.sh
#
# Preferred path: self-contained EF migration bundles built by CI
# (artifacts/efbundle-app[.exe], artifacts/efbundle-tenantstore[.exe]).
# Fallback: `dotnet ef database update` for both contexts (needs .NET SDK +
# the repository checked out at the deployed commit).
set -euo pipefail

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${ENV_FILE:-$REPO_DIR/.env.production}"

# shellcheck disable=SC1090
source "$ENV_FILE"

CONN="${ConnectionStrings__Database:?ConnectionStrings__Database (or POSTGRES_* in compose) must resolve. Set ConnectionStrings__Database explicitly for bundle runs.}"
export ConnectionStrings__Database="$CONN"

ARTIFACTS_DIR="${ARTIFACTS_DIR:-$REPO_DIR/artifacts}"

if [ -f "$ARTIFACTS_DIR/efbundle-app" ] && [ -f "$ARTIFACTS_DIR/efbundle-tenantstore" ]; then
  echo "==> Applying migrations via CI-built bundles"
  "$ARTIFACTS_DIR/efbundle-tenantstore" --connection "$CONN"
  "$ARTIFACTS_DIR/efbundle-app" --connection "$CONN"
else
  echo "==> No bundles in $ARTIFACTS_DIR; falling back to dotnet ef (SDK required)"
  dotnet ef database update \
    --project "$REPO_DIR/src/PharmaPOS.Infrastructure" \
    --startup-project "$REPO_DIR/src/PharmaPOS.Api" \
    --context TenantStoreDbContext
  dotnet ef database update \
    --project "$REPO_DIR/src/PharmaPOS.Infrastructure" \
    --startup-project "$REPO_DIR/src/PharmaPOS.Api" \
    --context AppDbContext
fi

echo "Migrations applied. Verify with:"
echo "  docker compose -f docker-compose.prod.yml --env-file $ENV_FILE exec postgres \\"
echo "    psql -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -c 'SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY 1;'"
