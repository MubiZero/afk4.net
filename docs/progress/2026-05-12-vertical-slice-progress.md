# AFK4 Current Progress Snapshot

Last updated: 2026-08-17

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
it is supplied at build time and git-ignored. Android credentials and the FCM
service account are configured, and delivery to a real device is verified (see
Latest Verification).

- **SMS through payom** — the gateway rejects free text, so the channel sends an
  approved template identifier plus placeholder values rather than a composed
  message. Template identifiers come from configuration
  (`Sms__TemplateIds__<key>`), because a template is immutable and editing its
  text yields a new identifier; a missing key makes the channel refuse
  permanently and name the variable instead of failing anonymously. The
  identifiers are configured and real delivery is verified.

- **Live-check corrections** — money in notifications is formatted the way the
  app formats it (player-language separator, currency sign instead of the ISO
  code), the seat screen explains an empty tariff list instead of showing a grey
  button, and the shift refusal travels as the machine code
  `open_shift_required` that interfaces already translate, replacing five
  hand-written variants of the same English sentence.

- **Staging deploy actually fires** — Coolify moved `/api/v1/deploy` to POST and
  answered GET with an error, so the workflow failed after every merge and had
  not deployed once since the API change while looking configured. Deployment
  status polling stayed on GET. Migrations are applied by the container's
  pre-deployment command at start rather than by a manual step.

- **Hour packages bought from the app** (revenue wave 2, slice 1) — the purchase
  existed but was a counter operation: it demanded an open shift and a staff
  actor, so prepaid time could only be bought by walking into the club, which is
  the opposite of what prepaying is for. The purchase core is now split by a flag
  the same way online top-up already is: the counter path requires a shift, the
  player path does not. When a shift does happen to be open the entries carry it,
  so the club still sees the revenue where it occurred. The actor is the reserved
  `Player Self-Service`; without it the cash journal printed a truncated empty
  guid. The short-wallet refusal travels as `insufficient_funds`, the name the
  same refusal already carries in the shop, in booking, and at session start.
  The app shows the price list with price, hours, bonus and validity, confirms
  the amount and the time before charging, and lists owned packages with the
  time left; spent and expired ones stay in the list, because a purchase that
  vanishes reads as money that vanished.

- **Group booking for a company** (revenue wave 2, slice 2) — a computer club is visited by a
  company, and the app booked one seat at a time. From the app the group is a seat
  **count**, not a list: the player never picks a machine, the club assigns it, so
  asking them to choose five would be asking about something they do not decide.
  (The operator-side group booking does take an explicit seat list — there a human
  drags across timeline rows and knows exactly which machines they are giving away.)
  All-or-nothing on money: the wallet must cover the whole company, otherwise not a
  single seat is booked, because seating half a company is worse than an honest
  refusal. Each seat carries its own hold rather than one shared hold for the group,
  so cancelling one seat, seating one person, and one no-show out of the company all
  run through exactly the same code as a single booking — nothing had to learn about
  groups. Pricing moved into one helper shared with single booking; two copies of it
  would eventually show a company one price and freeze another. The app grew a seat
  stepper, prices the whole company through the server, shows a group as one card
  with its seat count and total, and cancels the whole company in one request.

- **Refer a friend** (revenue wave 2, slice 3) — the club pays and the club sets the
  amounts, exactly as with cashback; off by default, because a loyalty programme
  switched on without the owner knowing starts giving away their money. The shape
  follows from one constraint: **players do not register themselves** — the club
  creates the account at the counter — so a code cannot be entered "at sign-up".
  The friend names it as a separate action in the app, once in the account's life.
  Payment is not for the code but for the friend's **first real top-up**: the code
  is a promise to come, the club pays for the arrival. A top-up below the club's
  minimum pays nothing and does not burn the promise — the next real one closes it.
  Guards: not your own code, not a second code, not an account older than the claim
  window, not a code from another club. The per-referrer cap stops paying the
  inviter but still pays the friend, who broke no rule and knew of no cap. Codes
  avoid look-alike characters (no O/0, no I/1) because they are spoken aloud and
  copied by hand. Both bonus entries ride the same transaction as the top-up that
  triggered them, the way cashback already does.

- **Online bookings are checked against the hall's machine count** — the app books
  without a seat (the club assigns the machine at seating), so the per-seat overlap
  check returned "no conflict" every time and a ten-machine hall accepted any number
  of bookings for one evening. The operator sorted it out with live people at the
  counter. Capacity asks the only question that means anything for a seatless
  booking: how many machines exist and how many are already promised. Capacity is
  seats with an attached, approved gaming PC — powered-off machines included, since
  at night nothing in the hall is on and it is tomorrow evening that gets booked.
  Occupancy counts seatless bookings one machine each, dedupes a seat-assigned
  booking against a session on the same seat, and ignores seated bookings whose
  machine a session already holds. The check applies to single and group bookings
  alike — a rule only for companies would make single bookings the loophole — and it
  runs before the money check, because telling someone they lack funds for an evening
  that has no machines sends them to top up for nothing.

- **Player sessions survive a night away** — a player who had not opened the app
  for a day met a connection error on a working connection, curable only by
  signing in again. Three faults stacked. The refresh token is single-use (the
  server revokes it and issues a new one), but the rotated session never reached
  disk because the client was constructed without `onSessionChanged` — the hook
  existed and nobody passed it — so storage kept a token the server had already
  revoked and the app died on the next launch. Concurrent requests each refreshed
  on their own: the first won and the rest presented the revoked token, failed,
  and wiped the session that had just been issued; refresh is now shared, and
  latecomers await the same result. A cleared session was observed by nobody, so
  the shell stayed put and blamed the connection; it now clears storage and shows
  the sign-in screen, which is the honest answer.

## Latest Verification

Older verification entries (2026-07-28 and earlier, including the superseded
Platform Control rebuild Tasks 1-7 gates) are archived in
`docs/archive/progress/2026-08-06-vertical-slice-detailed-history.md`.

- Booking capacity gate (2026-08-17): Platform API passed **2082 tests against a
  real PostgreSQL database with zero skips**; Shared Contracts 141/141;
  Localization 15/15; Building Blocks 3/3; Update Publisher 13/13; `@afk4/i18n`
  39/39; Organization Admin Web 1102/1102 plus its production build; Platform
  Control 286/286; the customer app passed 319 widget tests with a clean
  `flutter analyze`. The ten capacity tests were re-run with the check neutered:
  exactly the four refusal cases fail and the six "must still work" cases pass, so
  they prove the check rather than themselves; the two new booking-sheet tests were
  checked the same way. `AFK4.Agent.Service.Tests` fails 26 `ClientReleaseAutomation`
  tests here — verified identical on a clean tree, they need Windows signing tooling
  and run on the CI Windows job. The full-solution build was not run: the
  Windows-targeted projects cannot build on Linux. Not exercised on a device or
  against staging.

- Refer-a-friend gate (2026-08-16): Platform API passed **2072 tests against a real
  PostgreSQL database with zero skips**; Shared Contracts 141/141; Localization
  15/15; `@afk4/i18n` 39/39; Organization Admin Web 1102/1102 plus its production
  build; Platform Control 286/286; the customer app passed 317 widget tests with a
  clean `flutter analyze`. The migration `AddPlayerReferrals` adds two tables and a
  per-organization unique referral code on the player. The full-solution build was
  not run: the Windows-targeted projects cannot build on Linux. Not exercised on a
  device or against staging.

- Group booking gate (2026-08-16): Platform API passed **2060 tests against a real
  PostgreSQL database with zero skips**; Shared Contracts 141/141; Localization
  15/15; `@afk4/i18n` 39/39; Organization Admin Web 1102/1102 plus its production
  build; Platform Control 286/286; the customer app passed 307 widget tests with a
  clean `flutter analyze`. The full-solution build was not run: `AFK4.Player.Shell`
  and `AFK4.OrganizationAdmin.App` target Windows and cannot build on Linux. Nothing
  here has been exercised on a device or against staging.

- Hour packages and session survival gate (2026-08-15): Platform API passed
  **2051 tests against a real PostgreSQL database with zero skips** — the local
  database was raised for the run, so the 27 Postgres-only tests that earlier
  gates skipped are included here. Shared Contracts passed 141/141,
  Localization 15/15, `@afk4/i18n` 39/39, Organization Admin Web 1102/1102 plus
  its production build, Platform Control 286/286, and the customer app passed
  299 widget tests with a clean `flutter analyze`. The concurrent-refresh test
  was checked against the unfixed client and fails there, so it proves the race
  rather than itself. The full-solution build was not run: `AFK4.Player.Shell`
  targets Windows and cannot build on Linux. Nothing in this slice has been
  exercised on a real device or against staging yet.

  Two pre-existing defects were found and fixed on the way. The organization
  offboarding tests pinned the date 2026-08-10 and compared it with the system
  clock, so the "do not purge before the grace period ends" check silently
  inverted five days later — 2026-08-15 armed it, and it was already red on a
  clean tree. The i18n generator matched a placeholder name from the opening
  brace without requiring a comma or closing brace after it, so the first word
  of a plural branch became a placeholder: `other {valid for {count} days}`
  yielded an argument named `valid`. Cyrillic never matched the pattern, so the
  miss waited for the first English branch starting with a word; regenerating
  all three locales after the fix changed no existing string.

- Live Android device check (2026-08-15, run by the owner against Coolify
  staging, manual — no test artifact): payom template identifiers and the
  Android FCM credentials are configured, and the installed Android build
  carried a whole player scenario end to end. A real SMS code arrived and
  confirmed the phone number; the club owner topped up that player's wallet from
  the owner account; the player started a session and made a booking, including
  one for the following day, which behaved correctly. The push notifications for
  those scenarios arrived on the device. This supersedes the 2026-08-14 caveat
  below for Android: the FCM transport (JWT signing, token exchange, HTTP v1
  payload) and the payom transport are now exercised against the real gateways,
  not only around them. iOS/APNs delivery remains unverified — no APNs key and
  no iOS build exist yet.

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
  details wired into `NotificationOptions`. Email is now the only notification
  channel not proven against a real provider: SMS and Android push are.
- **iOS side of the mobile app** — no APNs key, no iOS build, no delivery
  evidence. Android is verified end to end; iOS is untouched.
- **Capacity is checked by machine count, so two cases stay open by design.** A
  branch with no attached, approved gaming PC is treated as unlimited — an
  unconfigured branch should not explain its own misconfiguration to a player. And a
  session with no scheduled end (`EndsAtUtc == null`, the ordinary walk-in) is not
  projected forward: a booking an hour out can be accepted next to one. Counting
  open-ended sessions as occupying the future would refuse tomorrow's bookings
  because the hall is full today, which is worse. Counter-side bookings
  (`CreateAsync`) are deliberately not capacity-checked: the operator sees the floor
  and may overbook on purpose.
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

The revenue wave for the mobile app is in progress; the rest is the Windows side
and the operational backlog.

1. Finish revenue wave 2 in the customer app. Slices 1-3 (hour packages, group
   booking, refer a friend) and the booking capacity check are done; the only one
   left is off-peak pricing. Contrary to the 2026-08-13 product analysis, tariffs
   carry **no** time windows — a tariff is a name plus a price per minute, and a
   human picks it. The model is decided: **a separate tariff selected automatically
   by schedule**, not time windows inside one tariff. A session is priced with one
   flat `TariffPricing` for its whole elapsed span, and the tariff version is frozen
   on the session; windows would force splitting elapsed time across them and
   re-deciding whether the minimum billable duration and the rounding increment
   apply per window or per session — a rewrite of `TariffBilling`, which the live
   accrued-cost display and the booking quote must agree with exactly. A scheduled
   tariff leaves `TariffBilling` untouched and adds only a resolver for "which
   tariff applies now in this branch". The accepted cost: a session started at 11:50
   on the cheap morning tariff runs to 15:00 at the morning price, which matches how
   the operator already picks a tariff today.
2. Return to the production-readiness backlog: repeat the
   Operator day flow from a clean `manager_workstation` install at 100%/125%,
   then run the physical Windows 10/11 gaming-PC Agent/Shell smoke. These need
   physical hardware and are the last functional evidence gap.
3. Wire real per-environment SMTP settings and work through the remaining
   pre-production decisions in the production-readiness roadmap.
4. Decide whether iOS is in scope before launch. If it is, it needs an APNs key,
   an Apple developer account, and a device pass equivalent to the Android one.
