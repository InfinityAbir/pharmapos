# Production go-live checklist

Sign-off gate for every production deploy. Check each box with evidence (command
output, screenshot, or test run); do not go live with unchecked boxes.

## A. Pre-deploy (staging or maintenance window)

- [ ] `docker compose -f docker-compose.prod.yml config` renders with production env.
- [ ] CI `production` job green: migration bundles build, proxy template renders.
- [ ] Fresh `./scripts/backup-db.sh` completed and size-checked.
- [ ] `./scripts/migrate-prod.sh` applied cleanly (both contexts); history tail inspected.
- [ ] `Database__SeedDemoData=false` and `Database__ApplyMigrationsOnStartup=false` effective
      (no `demo-pharmacy` tenant, no seed log lines).
- [ ] TLS valid: `curl -vI https://PUBLIC_HOST/` shows full chain, HSTS present.
- [ ] `./scripts/prod-smoke.sh https://PUBLIC_HOST` — ALL CHECKS PASSED.

## B. Functional verification (real tenant, real Keycloak users)

- [ ] 1. **Authentication and authorization** — login/logout, expired token rejected,
      cashier blocked from admin endpoints (403), permission change takes effect
      without re-login delay beyond token lifetime.
- [ ] 2. **Tenant isolation** — Tenant A session cannot read/write Tenant B
      (spot-check medicines, sales, reports + automated `TenantIsolationTests` green).
- [ ] 3. **POS and payment workflow** — scan/search → cart → discount → split tender
      (cash/card/bKash/Nagad) → receipt prints with correct totals and ৳ formatting.
- [ ] 4. **Inventory and stock consistency** — sale decrements batch stock (FEFO),
      movement ledger has a row per change, negative stock impossible (try to oversell).
- [ ] 5. **Purchase → inventory → sale flow** — create purchase → receive batches →
      stock increases → sell → stock decreases; full MVP chain per PRD §48.
- [ ] 6. **Returns** — return ≤ sold quantity enforced, stock restored, refund recorded,
      over-return rejected; returns unavailable offline with honest notice.
- [ ] 7. **Bangla/English localization** — language switcher flips labels, validation
      messages and receipts; medicine names stay in English per PRD.
- [ ] 8. **Offline/sync behavior** — airplane-mode sale queues, reconnect replays by
      idempotency key (no duplicate sale), conflict surfaces a 422 for manual
      resolution, queue survives reload.
- [ ] 9. **Reports and dashboard** — today's sales/transactions/revenue match receipts;
      low-stock/near-expiry lists accurate; date filters + pagination + CSV export work.
- [ ] 10. **Production database migrations** — `__EFMigrationsHistory` matches the
      deployed commit for both contexts; app boots with startup-migrate OFF.
- [ ] 11. **Docker deployment** — `docker compose ps` all healthy; image tags pinned
      in `.env.production`; proxy/api/web logs show no errors on boot.
- [ ] 12. **Health checks and error handling** — `/health`, `/health/ready`, `/healthz`
      green; forced 500 returns generic problem+json with `traceId` (no stack trace);
      anonymous call to a protected endpoint returns 401/403.

## C. Operational readiness

- [ ] Nightly backup cron installed; latest dump restorable (`restore-db.sh` drill
      into a scratch database within the last 90 days).
- [ ] Off-site backup copy verified (not only on the DB host's disk).
- [ ] Monitoring/alerts live: `/health/ready`, `/platform/info`, cert expiry,
      5xx rate, auth-failure spike.
- [ ] Runbook contacts + maintenance-window procedure posted (who runs §7, who
      approves promotion after a restore).
- [ ] Secrets inventory: every `yes` row in `docs/production.md §2` is unique to
      this environment; bootstrap Keycloak admin password rotated.

## Sign-off

| Role | Name | Date | Result |
|---|---|---|---|
| Deployer | | | |
| Pharmacy owner (UAT) | | | |
