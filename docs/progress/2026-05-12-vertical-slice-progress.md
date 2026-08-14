# AFK4 Current Progress Snapshot

Last updated: 2026-08-14

## Purpose

Compact current-state snapshot for new sessions — short enough to read every
time. Detailed historical notes and shipped implementation plans/specs live in:

- `docs/archive/progress/`
- `docs/archive/superpowers/plans/` and `docs/archive/superpowers/specs/`

Use archives only when historical evidence or old implementation context is
needed.

## Current Product Direction

- AFK4 is a cloud-first SaaS platform for computer clubs.
- Day-to-day club operations run in the native Windows app Organization Admin (a .NET
  shell hosting a WebView2 React/TypeScript UI).
- Platform-owner/support operations run in the browser Platform Control
  (`AFK4.PlatformControl.Web`, admin-only SPA under `/admin`).
- Players have a self-service shell + installable PWA portal (PIN/QR, balance,
  self-extend, online top-up).
- Backend is a .NET 10 ASP.NET Core modular monolith on PostgreSQL.
- Gaming PCs run the Windows Agent Service + Player Shell; manager workstations
  run the Organization Admin.

## Navigation

- Live plan/spec indexes: `docs/superpowers/plans/README.md`,
  `docs/superpowers/specs/README.md`.
- Architecture source of truth:
  `docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md`.
- Operational/production roadmap: `docs/roadmap/production-readiness.md`.

## Implemented (high level)

The full Platform Control redesign (both admin roles), SP3 platform administration +
SaaS billing, and the entire SP4 wave are implemented and merged to `main`:

- **Counter-loop / postpaid checkout** — open-tab postpaid, credit limits +
  auto-protection, session checkout links.
- **Anti-fraud controls** — manager review/approval, caps, daily owner summary.
- **Offline-resilience** — Agent grace mode, offline lease extension, command
  + billing outbox, adaptive heartbeat.
- **Customer portal (PWA) + customer shell** — player auth, self-service,
  online top-up.
- **Notifications backbone** — MailKit SMTP transport, dispatcher, outbox,
  contact fields + preferences; staff/owner password-reset backend.
- **Localization** — ru/en/tg catalog (`locales/*.json` + `packages/i18n`),
  per-branch locale.
- **Realtime-consistency** — SignalR device/operator clients, optimistic
  `Version`/409 on floor-map edits.
- **dcgate payments** — multi-tenant per-branch payment cards, AES-GCM secret
  storage, HMAC-verified webhook, owner card-onboarding cabinet (Subsystem A +
  B). Note: dcgate is player top-up/payments, separate from SaaS billing.

The Operator commerce/booking completion wave is merged to `main` and deployed
to Coolify staging:

- **Player Shop financial integrity** — new orders settle atomically as linked,
  idempotent paid POS sales with wallet, open-shift, receipt, immutable inventory
  cost, cancellation/refund, and sales-report COGS projections. A real PostgreSQL
  serializable-concurrency test deterministically holds both initial settlements
  after each reads the final stock unit and before commit; one transaction commits,
  while the other retries and returns `out_of_stock`. Whole-branch hardening now
  also preserves immutable stock/cost/currency snapshots through session checkout
  and refunds, uses the reserved `Player Shop` actor, and translates Shop
  transition/cancellation/refund save conflicts without partial finance. Product
  currency updates and every first inventory/sale-history writer now share a
  PostgreSQL serializable protocol; a deterministic first-sale/currency-update
  race proves that incompatible currencies cannot both commit.
- **Operator POS and inventory completion** — clearer price/balance/stock state,
  stable order disclosure motion, one shared Map/POS payment form, atomic mixed
  cash/wallet settlement, immutable original-mix refunds, linked ledger entries,
  and preserved cash-journal/receipt/anti-fraud access.
- **Operator booking and session start** — shared linked-client selection,
  Ctrl/Command multi-seat booking independent of current seat health, explicit
  pending confirmation, optimistic reservation versions, and one atomic,
  idempotent session start linked back to the confirmed reservation. PostgreSQL
  overlap tests prove one effect set for concurrent reservation commands and for
  reservation start racing an ordinary start on the same seat; rollback and audit
  failures leave no partial session, billing, lease, command, or reservation state.
- **Native Operator P0 day-flow gate** — a real Windows WPF/WebView2 host completed
  sign-in, shift open/close, club-client selection, prepaid session start, confirmed
  reservation start, mixed cash/wallet POS payment, receipt refund, restored stock,
  zero-bill prepaid checkout, forced reload, and authoritative reconnect against
  Coolify staging. The smoke found and fixed missing SignalR handlers on secondary
  Operator connections, a cross-type `POS-*` receipt-number collision, and prepaid
  session time being charged again at checkout. Live PostgreSQL reconciliation proved
  sequential `POS-...-0001/0002/0003` receipts for the mixed sale and session checkouts;
  the final fresh prepaid session produced exactly one `gameplay_charge=-300`, no
  checkout payment/debt entries, and a zero checkout quote.
- **Operator post-auth shift gate** — staff with `shifts.open` now remain on an
  authoritative, non-dismissible shift-opening screen after interactive sign-in or
  native session restore until an existing shift is confirmed or a new shift is
  opened. The floor map, workspaces, preloading, and realtime stay inactive while
  gated; staff without that permission continue directly, and permanently
  unauthorized rail sections are hidden instead of shown as disabled controls.
- **Operator cash terminal redesign** — `Смена`, cash operations, receipts,
  approvals, and audit now share a dense register/inspector grammar with stable
  selectable rows, permission-derived navigation, responsive inspectors, and
  backend-confirmed money actions. The shift screen follows an operational
  command-center hierarchy: status, prominent drawer reconciliation, revenue,
  readable cash movements, past shifts, compact exports, and a quieter two-sided
  shell footer. Receipt-only staff retain direct journal access, and dark/light
  rendered QA covers 1920, 1440, 1280, and the narrow stacked state.
- **Authoritative Operator system footer** — the native auth contract carries
  ordered real staff roles through protected token restore and refresh, while
  the WebView bootstrap exposes the installed host assembly version. The
  one-row footer shows the authenticated operator, localized roles,
  authoritative current club, independent SignalR/backend health, version, and
  minute-aligned local time; missing values degrade to an em dash instead of
  fabricated data.

Plus the earlier base: identity/tenancy/RBAC/audit, devices/floor-map,
session lifecycle + leases, ledger/POS/shifts/reports, update publishing
+ rollout, and the Agent/Setup-Wizard/Player-Shell/packaging stack. Staff
(including organization owners) sign in by phone or email/login and password;
there is no owner-code mechanism.

- **Organization Admin Reports redesign** — `Отчёты` now contains only
  `Сводка`, `Смены и касса`, and `Выручка`; audit evidence stays in `События`.
  Dedicated branch-scoped API projections resolve local dates through the
  branch timezone, return backend-owned full-range totals, cap the attention
  preview while preserving its total, expose a seven-day trend and active-shift
  context, compare revenue with the previous equivalent period, break revenue
  down by source/payment method/staff, and produce one CSV per visible report.
  Failed critical money actions and closed-shift discrepancies feed Summary.

- **Organization Admin safe update controls** — organization owners can inspect
  installed/offered Admin App versions, safe progress or failure detail, and the
  branch maintenance window without receiving Platform Control publication or
  rollout powers. A permission-gated window editor persists through the branch
  API; `Перезапустить и обновить` binds the exact rollout/package to a native
  acknowledgement, refuses to close during critical work, and then lets Agent
  install after graceful shutdown. Organization Admin Web passed 1039 tests and
  its production build; i18n passed 39/39, Windows App passed 254/254 on the
  Windows runtime, and the full solution build passed with 0 warnings/errors.
- **Manager-workstation update provisioning** — Setup now writes the installed
  Admin App path plus one device-bound pipe name and HMAC-derived coordination
  secret to Agent bootstrap and matching native-app environment variables; the
  source device credential is not reused as the pipe secret or logged. Fresh
  Windows gates passed Agent 201/201 and Setup Wizard 33/33. A real unsigned
  internal package build produced the master client installer and verified the
  bundled Organization Admin MSI contents from a drive-letter checkout. This
  host has no installed/enrolled AFK4 Agent or Admin App, so the closed,
  idle-open, and critical-command-open physical rollout scenarios remain
  unclaimed and are specified in the real-device smoke runbook.

- **Operator `/club` functional parity closure** — Clients, Monetization,
  Settings, and Venue gaps are closed in native Operator surfaces: complete staff
  role sets, lifecycle-safe client actions, time corrections and partial refunds,
  package context and wallet-backed Cash sales, reusable product categories,
  independent package load errors, and device rename/remove. The 2026-07-28
  parity certificate is GO for a separate Platform Control `/club` removal project.
- **Platform Control `/club` removal** — the obsolete browser club workspace,
  staff-auth runtime, branch-scoped API client, club-only shell, and audience
  build switch are removed. `AFK4.PlatformControl.Web` now contains only the internal
  `/admin` Platform Control; old `/club/*` and browser staff sign-in/reset URLs
  return the explicit not-found screen. Public first-owner invite acceptance
  remains as a stateless onboarding page and sends the owner to Organization Admin.
  Shared money conversion moved to `src/lib`, while backend branch endpoints
  and Operator contracts remain unchanged.

- **Platform Control rebuild review hardening** — the operational overview now
  receives fixed-query-count organization attention projections for recent
  denied operations, owner invitations expiring within three days, and failed
  device rollouts, alongside suspension and past-due billing signals. Optional
  SaaS billing failure remains visible as a retryable partial state. Overview
  data loads only on the overview route and only within the caller's billing
  permission. Direct URLs to forbidden organization tabs now show an explicit
  access boundary with a safe Summary action. Remaining organization sorting
  and rollout-target labels are fully localized in ru/en/tg.
- **Platform Control panel redesign** — the browser panel now opens on a
  fleet-pulse home screen (network-to-club signal rows) instead of an
  organization list/registry; an organization/club selection opens a
  master-detail client card built around a passport view with permission-
  filtered tabs, replacing the earlier registry-style workspace (the old
  Summary/Clubs/Owners/Subscription/Invoices/Support/History tab set and the
  standalone registry screens are retired). The panel was moved onto the
  shared `@afk4/ui` component kit (forms, fields, headers, panels) on the same
  pattern as Organization Admin, and onto shared AFK4 design tokens.
- **Platform administrator directory + mandatory 2FA** — platform roles
  (platform admin/support) sign in through a two-step flow: password, then a
  dependency-free RFC 6238 TOTP code; 2FA is mandatory for platform roles, not
  optional. A platform staff directory screen replaced the former settings
  placeholder, backed by a dedicated staff sub-client and API. Full loss of a
  platform admin's 2FA has a documented recovery runbook.
- **Support mode** — a bounded, audited support-access mode lets platform
  support staff obtain a temporary, ticket-bound session against a customer
  organization/club (atomic ticket-to-session exchange) without a standing
  organization-scoped account, with the access boundary enforced through
  existing role checks rather than a parallel authorization path.

- **Club showcase, reviews, and player record (mobile app)** — the public club
  catalogue now carries a shop window (hall photo, city/address, price-from,
  seat count, rating) plus map coordinates, filled in by the owner on the
  operator "Клуб" screen. The player app's club picker renders that as photo
  cards with a list/map toggle (flutter_map over OpenStreetMap tiles, with
  attribution); clubs without coordinates stay in the list and are simply
  absent from the map. Reviews are tied to a visit — one ended session, one
  review — surfaced as a post-visit prompt on the dashboard and readable
  before sign-in from the club card. A player record screen derives level,
  hours played, and achievements from visit history; nothing about it is
  stored separately. The club card also answers "is it open right now" from
  the branch schedule (which now accepts overnight shifts such as 22:00-06:00,
  the normal case for a computer club), and carries a swipeable hall gallery —
  up to ten photos per branch, uploaded and ordered on the operator screen. A
  club-details sheet behind "Подробнее" carries the owner's description, the
  halls with their hardware (a new per-zone field edited in «Залы и ПК»), and
  the week's schedule, and the club can be chosen from there directly.

Push notifications reach the player's phone through the existing notification
backbone rather than around it: `Push` is a channel alongside email and SMS, so
it reuses the same templates, outbox, idempotency and backoff. It is addressed
by player account rather than by token — a player may have a phone and a
tablet, and one queue row fans out to every registered device; a token FCM
reports as unregistered is deleted instead of accumulating failures forever.
Four triggers are wired: a session ending in ten minutes (while extending is
still possible), a booking an hour out, a fulfilled top-up, and an accepted
shop order. The first two have no event to hang off and are found by clock in a
periodic job, keyed per session and per reservation so frequent ticks cannot
ring twice. Delivery failures never fail the operation that caused them. The
app registers its device on sign-in, removes it on sign-out, follows FCM token
rotation, and carries a real off switch in the profile — off means the device
is removed server-side, not a flag hidden in the app. FCM credentials live in
environment variables; without them the channel stays silent and the server
runs normally. `google-services.json` is deliberately not in the repository —
it is supplied at build time and git-ignored.

## Latest Verification

Older verification entries (2026-07-28 and earlier, including the superseded
Platform Control rebuild Tasks 1-7 gates) are archived in
`docs/archive/progress/2026-08-06-vertical-slice-detailed-history.md`.

- Push notification gate (2026-08-14): Platform API passed 1996 tests with 27
  PostgreSQL-only skips; Shared Contracts passed 141/141; Organization Admin
  Web passed 1102 tests; i18n passed 39/39; the customer app passed 286 widget
  tests with a clean `flutter analyze`. Delivery to a real phone was NOT
  verified: it needs an APNs key, a Firebase service-account key, and a device
  build, none of which exist in this environment. What is covered by tests is
  the logic around delivery — channel fan-out, dead-token cleanup, reminder
  windows and idempotency, device registration and removal, and the app's
  register/unregister/rotate behaviour. The FCM transport itself (JWT signing,
  token exchange, HTTP v1 payload) is unexercised until credentials exist.

- Club showcase / reviews / player record gate (2026-08-13): Platform API
  passed 1973 tests with 27 PostgreSQL-only skips; Shared Contracts passed
  141/141; Organization Admin Web passed 1102 tests; i18n passed 39/39; the
  customer app passed 277 widget tests with a clean `flutter analyze`. The
  full-solution build was not run on this machine: `AFK4.Player.Shell` targets
  Windows and cannot build on Linux. A live browser pass over the picker,
  reviews, review sheet, and record screen was done against a local fake API;
  OpenStreetMap tiles are unreachable from this environment, so the map was
  verified by its pins and camera fit, not by rendered tiles. The hall gallery
  was verified by widget test (swipe + tap) and by its page dots in the
  browser: Flutter leaves mouse out of `dragDevices`, so a desktop-web mouse
  cannot swipe it — touch can, and this app ships to phones.

- Platform-admin directory, mandatory 2FA, and support-mode gate (2026-08-06):
  the Platform API suite passed 1596 tests against a real PostgreSQL database
  with zero skips; Organization Admin Web passed 993 component/model tests
  plus 94 App integration tests; Platform Control passed 174/174; i18n passed
  39/39; and the full solution build completed cleanly.

- Platform Control rebuild review gate (2026-07-30): Platform Control passed
  147/147 tests and its production build; i18n passed 39/39; Shared Contracts
  passed 137/137; Platform API passed 1477 tests with 14 PostgreSQL-only skips;
  and the sequential full solution build completed with 0 warnings and 0
  errors. The attention projection integration test covers all three added
  operational counts without per-organization requests. No push, merge,
  deployment, or physical Windows smoke was performed.

## Known Gaps

- **Rendered Reports QA** — the redesigned Organization Admin Reports views
  have automated component/App coverage and a green production build, but still
  need a native WebView2 visual pass at 100%/125% scaling in dark and light
  themes together with the broader clean `manager_workstation` smoke below.

- **Per-environment SMTP config** still needs the user's real connection
  details wired into `NotificationOptions`.
- **Operator entity search** is still deferred: the command palette navigates
  between workspaces but does not yet search clients, seats, reservations,
  orders, or receipts.
- **Remaining Windows evidence** is narrower: repeat the Operator pass on a clean
  `manager_workstation` install at 100%/125% scaling and run the physical Windows
  10/11 gaming-PC smoke for lock/unlock enforcement, reboot recovery, and
  role-aware update/rollback. The manager workstation must also prove the Admin
  App update lifecycle with the app closed, idle-open, and holding a critical
  command. The WindowsDesktop, provisioning, and unsigned package-build gates
  are already green; this remaining item is physical-device UX/runtime evidence.
- **Rotate the former staging smoke credential** before the next staging smoke;
  it was removed from the tracked runbook and must be supplied only through the
  approved secret store.
- **Pre-production release decisions** remain: Authenticode custody, production
  object store/CDN, presigned upload automation, package-registration
  credentials, staging secret rotation, backup/restore ownership — tracked in
  `docs/roadmap/production-readiness.md`.

## Recommended Next Work

1. Return to the production-readiness backlog: repeat the
   Operator day flow from a clean `manager_workstation` install at 100%/125%,
   then run the physical Windows 10/11 gaming-PC Agent/Shell smoke.
2. Wire real per-environment SMTP settings and work through the remaining
   pre-production decisions in the production-readiness roadmap.
