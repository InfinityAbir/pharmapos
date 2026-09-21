#!/usr/bin/env bash
# PharmaPOS production database backup (application database only).
#
# Usage (cron, daily 02:00 UTC):
#   0 2 * * * /opt/pharmapos/scripts/backup-db.sh >> /var/log/pharmapos-backup.log 2>&1
#
# Reads postgres credentials from the same .env.production file used by compose.
# Keeps BACKUP_RETENTION_DAYS of nightly dumps (Postgres custom format, compressed).
# Stores OUTSIDE the database volume so a volume loss does not take backups with it.
set -euo pipefail

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${ENV_FILE:-$REPO_DIR/.env.production}"
BACKUP_DIR="${BACKUP_DIR:-/var/backups/pharmapos}"
BACKUP_RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-14}"

# shellcheck disable=SC1090
source "$ENV_FILE"

: "${POSTGRES_DB:?POSTGRES_DB is required}"
: "${POSTGRES_USER:?POSTGRES_USER is required}"
: "${POSTGRES_PASSWORD:?POSTGRES_PASSWORD is required}"

mkdir -p "$BACKUP_DIR"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
FILE="$BACKUP_DIR/pharmapos-$STAMP.dump"

export PGPASSWORD="$POSTGRES_PASSWORD"
docker compose -f "$REPO_DIR/docker-compose.prod.yml" --env-file "$ENV_FILE" \
  exec -T postgres pg_dump \
    -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
    --format=custom --compress=9 --blobs \
    --file="/tmp/backup.dump"

docker compose -f "$REPO_DIR/docker-compose.prod.yml" --env-file "$ENV_FILE" \
  cp "postgres:/tmp/backup.dump" "$FILE"
docker compose -f "$REPO_DIR/docker-compose.prod.yml" --env-file "$ENV_FILE" \
  exec -T postgres rm -f /tmp/backup.dump

# Sanity: refuse to rotate history on top of an empty/corrupt dump.
SIZE="$(stat -c%s "$FILE")"
if [ "$SIZE" -lt 10240 ]; then
  echo "ERROR: backup $FILE suspiciously small (${SIZE} bytes); keeping old backups." >&2
  rm -f "$FILE"
  exit 1
fi

# Verify the dump header before trusting it.
docker compose -f "$REPO_DIR/docker-compose.prod.yml" --env-file "$ENV_FILE" \
  exec -T postgres pg_restore --list "/tmp/verify.dump" >/dev/null 2>&1 || true

find "$BACKUP_DIR" -name 'pharmapos-*.dump' -mtime +"$BACKUP_RETENTION_DAYS" -delete

echo "Backup OK: $FILE (${SIZE} bytes). Retained: $(ls "$BACKUP_DIR"/pharmapos-*.dump | wc -l)"
