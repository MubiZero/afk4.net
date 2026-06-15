---
name: ux-audit-roadmap
description: "2026-06-01 full-product UX audit → tiered roadmap; as of 2026-06-04 the roadmap is essentially executed (all tiers landed in SP4). Kept for the durable audit findings only."
metadata:
  node_type: memory
  type: project
  originSessionId: ef59361c-2632-4595-9c60-706331a96b60
---

On 2026-06-01 ran a full-product UX audit of afk4.net (platform admin, club owner, operator, staff, POS, customer + cross-cutting). Produced 9 design specs in `docs/superpowers/specs/2026-06-01-platform-*.md` (now tracked) and a tiered roadmap.

**As of 2026-06-04 the roadmap is essentially DONE** — verified against code, not PR titles:
- **counter-loop / postpaid checkout** (Tier 1) — migrations `AddPostpaidCreditLimitsAndAutoProtection`, `AddSessionCheckoutLinks`; floor-map seat panels + `ISessionBillingService`.
- **anti-fraud-controls** (Tier 0) — manager review + daily owner summary (plan `2026-06-03-anti-fraud-manager-review-react.md`).
- **offline-resilience** (Tier 0) — `AFK4.Agent.Service`: `GraceModeMonitor`, `OfflineGraceState`, `OfflineLeaseExtender`, `FileCommandResultOutbox`, `HeartbeatCadence` + `AddBillingOutbox` migration.
- **customer-portal** (PWA) + **customer-shell-experience** (Tier 2) — PR #49/#50, plans `customer-portal-*` / `customer-shell-*`.
- **notifications-backbone** (Tier 2) — `Notifications/` (MailKit SMTP transport, dispatcher, outbox), contact-field + preference migrations.
- **localization** ru/en/tg (Tier 2) — `locales/*.json` + `packages/i18n`.
- **realtime-consistency** (Tier 3) — SignalR `DeviceRealtimeClient`/`OperatorRealtimeClient`, optimistic `Version`/409 in FloorMap.
- **payments (dcgate)** — added on top of the roadmap: Subsystem A + B, per-branch cards, AES-GCM secrets, HMAC webhook, online top-up.

**Known remaining gap:** FE forgot/reset-password screen is still a `ReservedAuthPage` placeholder even though the backend reset path is wired — see [[email-server-available]].

**Durable findings (still true):** money handling is correct (long minor units, conversion at UI boundary) — low risk. See [[platform-web-redesign]] for engineering patterns.
