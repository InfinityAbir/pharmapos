#!/usr/bin/env bash
# PharmaPOS production smoke test — run after every deploy and after recovery.
# Fails (non-zero exit) on the first unhealthy signal. Safe to run repeatedly.
set -euo pipefail

BASE_URL="${1:-https://localhost}"
echo "==> Smoke-testing $BASE_URL"

pass() { echo "  PASS: $1"; }
fail() { echo "  FAIL: $1" >&2; exit 1; }

# 1. Proxy is up (its own probe; no upstream involved).
curl -fsS --max-time 10 "$BASE_URL/healthz" | grep -q '^ok$' \
  && pass "proxy /healthz" \
  || fail "proxy /healthz"

# 2. API liveness (process up).
curl -fsS --max-time 10 "$BASE_URL/health" \
  && pass "api /health (liveness)" \
  || fail "api /health (liveness)"

# 3. Readiness: PostgreSQL reachable from the API.
curl -fsS --max-time 15 "$BASE_URL/health/ready" | grep -qi 'healthy' \
  && pass "api /health/ready (postgres)" \
  || fail "api /health/ready (postgres)"

# 4. Anonymous platform surface (also the SPA offline heartbeat target).
curl -fsS --max-time 10 "$BASE_URL/api/v1/platform/info" | grep -qi 'pharmapos' \
  && pass "api /api/v1/platform/info" \
  || fail "api /api/v1/platform/info"

# 5. Authenticated surface rejects anonymous callers (never 2xx without a token).
CODE="$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 "$BASE_URL/api/v1/tenant/sales" || true)"
if [ "$CODE" = "401" ] || [ "$CODE" = "403" ]; then
  pass "protected endpoint rejects anonymous ($CODE)"
else
  fail "protected endpoint returned $CODE (expected 401/403)"
fi

# 6. SPA shell served with no-store entry point.
curl -fsSI --max-time 10 "$BASE_URL/" | grep -qi 'text/html' \
  && pass "spa shell" \
  || fail "spa shell"
curl -fsS --max-time 10 "$BASE_URL/config.json" | grep -q 'apiBaseUrl' \
  && pass "spa /config.json" \
  || fail "spa /config.json"

# 7. Security headers present at the edge.
HEADERS="$(curl -fsSI --max-time 10 "$BASE_URL/" || true)"
echo "$HEADERS" | grep -qi 'strict-transport-security' \
  && pass "HSTS" \
  || fail "HSTS header missing"
echo "$HEADERS" | grep -qi 'content-security-policy' \
  && pass "Content-Security-Policy" \
  || fail "Content-Security-Policy header missing"
echo "$HEADERS" | grep -qi 'x-content-type-options: nosniff' \
  && pass "X-Content-Type-Options" \
  || fail "X-Content-Type-Options header missing"

echo "ALL SMOKE CHECKS PASSED."
