#!/usr/bin/env bash
# PharmaPOS point-in-time restore drill / disaster recovery.
#
# Usage:
#   ./scripts/restore-db.sh /var/backups/pharmapos/pharmapos-20260920T020000Z.dump
#
# Restores into a SEPARATE database on the same server, verifies migrations are
# current, and prints the go/no-go checklist. It never overwrites the live
# database: promotion to live is an explicit manual step (see docs/production.md).
set -euo pipefail

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${ENV_FILE:-$REPO_DIR/.env.production}"
BACKUP_FILE="${1:?Usage: restore-db.sh <backup.dump>}"

# shellcheck disable=SC1090
source "$ENV_FILE"

: "${POSTGRES_DB:?POSTGRES_DB is required}"
: "${POSTGRES_USER:?POSTGRES_USER is required}"
: "${POSTGRES_PASSWORD:?POSTGRES_PASSWORD is required}"

RESTORE_DB="${RESTORE_DB:-${POSTGRES_DB}_restore_$(date -u +%Y%m%dT%H%M%SZ)}"
export PGPASSWORD="$POSTGRES_PASSWORD"
COMPOSE="docker compose -f $REPO_DIR/docker-compose.prod.yml --env-file $ENV_FILE"

echo "==> Creating restore target database: $RESTORE_DB"
# shellcheck disable=SC2086
$COMPOSE exec -T postgres psql -U "$POSTGRES_USER" -d postgres -c "CREATE DATABASE \"$RESTORE_DB\";"

echo "==> Copying backup into the container and restoring"
$COMPOSE cp "$BACKUP_FILE" postgres:/tmp/restore.dump
# shellcheck disable=SC2086
$COMPOSE exec -T postgres pg_restore \
  -U "$POSTGRES_USER" -d "$RESTORE_DB" \
  --no-owner --role="$POSTGRES_USER" /tmp/restore.dump
$COMPOSE exec -T postgres rm -f /tmp/restore.dump

echo "==> Verifying: tenants, medicines and migration history present"
# shellcheck disable=SC2086
$COMPOSE exec -T postgres psql -U "$POSTGRES_USER" -d "$RESTORE_DB" -c \
  "SELECT count(*) AS tenants FROM \"TenantInfo\";" \
  -c "SELECT count(*) AS medicines FROM \"Medicines\";" \
  -c "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 8;"

echo ""
echo "Restore drill complete into database [$RESTORE_DB]."
echo "Next (manual, during a maintenance window):"
echo "  1. Stop the stack:           $COMPOSE down"
echo "  2. Rename live DB aside and rename $RESTORE_DB to $POSTGRES_DB"
echo "  3. Apply pending migrations: ./scripts/migrate-prod.sh"
echo "  4. Start the stack and run:  ./scripts/prod-smoke.sh"
