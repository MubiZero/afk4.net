---
name: dcgate-online-topup-shift-fix
description: Money bug — dcgate online top-up webhook required an open shift (503), so paid money never credited. Fix SHIPPED & e2e-verified on staging 2026-06-08. Test-suite speedup DONE: shared host + ServerGC + log-silencing, 573s→~27s (~20×), suite green.
metadata: 
  node_type: memory
  type: project
  originSessionId: 29fa6666-3b5c-4c0e-8acc-314811021a2e
---

## Thread 1 — money bug fix: DONE & SHIPPED & e2e-verified (2026-06-08)

Live e2e on staging ([[coolify-reference]], API `https://afk4.staging.mubi.dev`) ran the full dcgate
online top-up with a REAL card payment. Bug: dcgate confirmed `paid` → `payment.paid` webhook → afk4
returned **503 ×4** because the webhook reused `TopUpWalletAsync` → `RequireOpenShiftAsync`, but online
money arrives 24/7 with no cashier shift open. Real money paid, balance stayed 0.

Fix (Option 1): added `IBillingCommandService.CreditOnlineTopUpAsync` — records the ledger entry with
`ShiftId = null` and `CreatedByStaffUserId = Guid.Empty` (no shift, no actor). Shift cash reports only
count entries WITH a shift, so null online entries are correctly excluded. Webhook now calls it.
Implemented via extracted `TopUpWalletCoreAsync(..., bool requireOpenShift)`.

**Shipped:** commits `a921dcb` (fix + e2e runbook `docs/operations/e2e-staging-dcgate-runbook.md`) and
`725de0f` (Thread 2 logging) pushed to origin/main. GitHub Actions `coolify-staging-deploy.yml`
auto-deployed to staging (success, health verified). Then retried the already-paid dcgate webhook event
`cmq59mo1k0003ml016kf01a5p` via `POST https://dcgate.mubi.dev/api/admin/webhook-events/{id}/retry`
(header `x-admin-secret` = dcgate Coolify app `tk5e553sv2adamz7p2505a6y` env `ADMIN_JWT_SECRET`).
**Result: player `+992900000001` balance 0 → 1000 minor units (10 TJS), intent `fulfilled`.** Money bug
closed end-to-end. Backend suite 1101/1101 green.

## Thread 2 — test-suite speedup: DONE. 573s → ~27s (~20×). Shared host shipped.

Backend xunit suite (1101 tests, 8 cores). Three changes, all shipped to main, suite green 1101/1101:
- Logging silencing (`725de0f`): `ClearProviders()`+`Warning`. 573→534s.
- Server GC (`e18c37b`): `<ServerGarbageCollection>true</...>` in test csproj. 534→490s.
- **Shared host (`60f4842`): 490→~27s. The big one.** `PlatformApiFactory` is now a thin wrapper over
  ONE shared `WebApplicationFactory<Program>` instead of building a host per test (~450 builds × ~650ms,
  which sat on every serial-in-class critical path — that's why it was the real bottleneck, not the
  "14% of CPU" my earlier profiling implied. Earlier "poor ROI" call was WRONG.)

**How shared-host isolation works (so don't relearn it the hard way):**
- Unique InMemory DB name per test in an `AsyncLocal`; DbContext registered with `optionsLifetime:
  Scoped` so every request/seeding scope re-reads it; `Server.PreserveExecutionContext = true` carries
  the AsyncLocal into TestServer request handling. Call sites unchanged (`new()/.CreateClient()/.Services`).
- Hosted services REMOVED from the shared host (timers would run forever vs one DB); the real
  `BillingPlanSeedHostedService` is run per fresh DB via `SeedBaseline()` (no data duplication, rule #37).
- The IP-partitioned rate limiter + install throttle were the trap: TestServer leaves RemoteIpAddress
  empty → all tests shared one partition → 429s (48 of the 49 first-round failures). Fix: an
  `IStartupFilter` stamps a unique **private-range 10.x.x.x** IP per test (monotonic counter, not a name
  hash — avoids partition collisions). Private-range so the app still trusts `X-Forwarded-For` (that was
  the last failure: `InstallEndpointTests` asserts the forwarded IP).
- Tests with `useRealSessionBilling:true` or `extraServices` (~12) still build their own throwaway host.
- Verified stable: 4 consecutive green full runs (24–30s). If a NEW shared-singleton leak surfaces
  (cache/store bleeding across tests), the pattern is: give that test its own host, or reset/partition
  the singleton per-test like the IP fix.
