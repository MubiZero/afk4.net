---
name: afk4-time-handling-audit
description: "Project-wide time/clock handling audit — DONE; cheap hardening shipped in PR#69 (merged+deployed). Money is server-authoritative and safe; the risky lease/grace enforcement rewrite is deliberately deferred until drift logs show real fleets drift."
metadata:
  node_type: memory
  type: project
  originSessionId: 419d4960-82a3-41b8-af9f-4fb3db04624d
---

Read-only audit (4 parallel agents) of time/clock handling across session-billing, hour-extension, leases, grace, reports, UI boundaries — done 2026-06-10.

**Headline: money is safe.** Billing & hour-extension are server-authoritative and duration-based. Checkout charges `now − StartedAtUtc` on the SERVER clock; extension adds minutes to `EndsAtUtc` server-side. The player shell only POSTs `/extend`; leases are ECDSA-signed; tariffs are flat per-minute (no time-of-day pricing). A tampered gaming-PC clock can only distort the on-screen countdown, never the bill or granted time. The codebase is already hygienic (`DateTimeOffset …Utc` + injected `TimeProvider`, not raw `DateTime.Now`).

**Real risks were skew & implicit-tz, not timezones per se. Cheap/safe subset SHIPPED in PR #69 (merged to main + deployed to staging 2026-06-10):**
- **Clock-drift visibility:** server already sends `ServerTimeUtc` in the heartbeat response but the Agent ignored it. New pure helper `src/AFK4.Agent.Service/ClockDriftCheck.cs`; Worker now injects `TimeProvider`, compares each heartbeat, logs WARN at >30s drift (log-only, no enforcement change). All raw `DateTimeOffset.UtcNow` in Worker.cs routed through `TimeProvider`.
- **Hygiene:** all server-side raw `DateTimeOffset.UtcNow` in Platform.Api routed to injected `TimeProvider.GetUtcNow()` (0 raw reads left in Platform.Api production).
- **Timezone marker (additive, no behavior change):** `BranchEntity.PreferredTimeZone` (string, default `"Asia/Dushanbe"`, maxLen 64) + migration `20260610041536_AddBranchPreferredTimeZone`. Makes a future multi-region pass additive.
- **Business-day fix:** `EfDailySummaryRunner` now resolves "yesterday" in `BusinessDayOptions.TimeZone` (default `Asia/Dushanbe`, config section `BusinessDay`, new `src/AFK4.Platform.Api/Reports/BusinessDayOptions.cs`) instead of UTC — night takings land in the right day. (Previously sliced the day at UTC = 05:00 Dushanbe, so night-club revenue landed in the wrong day even single-region.)
- Migration `AddBranchPreferredTimeZone` was applied BY HAND on staging (afk4 does not auto-migrate). Shift-revenue "Смены" operator screen shipped in the same PR — see [[afk4-multitenant-payments-state]] context only; details in git.

**STILL DEFERRED (deliberately, measure first):**
- **Offline-grace / lease offset-correction** (the actual "cure" for clock-skew): hold until the new drift WARN logs show real fleets actually drift. Don't rewrite PC-lock enforcement on a theoretical risk. (Skew risks identified: heartbeat lease-refresh compares Agent-reported expiry vs server `now+5min`; offline grace measured on the local Agent clock — both bounded, consequence is refresh churn / bounded grace, not money.)
- **Full multi-region tz conversion** (UI `datetime-local` inputs interpret in browser-local tz; server day-boundaries by branch tz) — YAGNI until a real 2nd region; safe for single-region Tajikistan (UTC+5, no DST). The marker field makes it additive later. Sharpest spot: customer `ReservationsScreen.tsx` + operator news `datetime-local`.

See [[afk4-sp4-shipped]], [[afk4-customer-shell-pivot]].
