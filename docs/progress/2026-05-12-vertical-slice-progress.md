# AFK4 Current Progress Snapshot

Last updated: 2026-09-24

## Purpose

Compact current-state snapshot for new sessions — short enough to read every
time. Detailed historical notes and shipped implementation plans/specs live in:

- `docs/archive/progress/`
- `docs/archive/superpowers/plans/` and `docs/archive/superpowers/specs/`

Use archives only when historical evidence or old implementation context is
needed.

## Current Product Direction

- AFK4 is a cloud-first SaaS platform for computer clubs.
- Day-to-day club operations **and** the owner-facing settings run in the same
  native Windows app, Organization Admin (a .NET shell hosting a WebView2
  React/TypeScript UI) — permissions decide who sees which sections, not which
  app they open. It was the Operator App before the 2026-07-28 product-boundary
  migration; that name survives only inside the code and in archived plans.
- Platform-owner/support operations run in the browser Platform Control
  (`AFK4.PlatformControl.Web`, admin-only SPA under `/admin`).
- Players use the Flutter app (`src/afk4_customer_app`) on their phone and the
  Player Shell on the gaming PC itself. The separate React player web
  (`AFK4.Customer.Web`) was removed on 2026-09-02: the Flutter app already builds
  for the web, so a link without an install is covered by the same app.
  iOS is not built at all yet — Android and web only.
- Backend is a .NET 10 ASP.NET Core modular monolith on PostgreSQL.
- Gaming PCs run the Windows Agent Service + Player Shell; manager workstations
  run the Organization Admin.

## Navigation

- Live plan/spec indexes: `docs/superpowers/plans/README.md`,
  `docs/superpowers/specs/README.md`.
- Architecture source of truth:
  `docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md`.
- Operational/production roadmap: `docs/roadmap/production-readiness.md`.
- Opening Organization Admin against a deployed environment, and which of its
  sections holds what: `docs/operations/organization-admin-access.md`. Read it
  before looking for an "owner cabinet" — there isn't one, the owner-facing
  settings are the `Управление` workspace inside Organization Admin, and Platform
  Control is a different product.

## Implemented (high level)

The full Platform Control redesign (both admin roles), SP3 platform administration +
SaaS billing, and the entire SP4 wave are implemented and merged to `main`:

- **Counter-loop / postpaid checkout** — open-tab postpaid, credit limits +
  auto-protection, session checkout links.
- **Anti-fraud controls** — manager review/approval, caps, daily owner summary.
- **Offline-resilience** — Agent grace mode, offline lease extension, command
  + billing outbox, adaptive heartbeat.
- **Player shell on the gaming PC** — player auth, self-service extend, shop,
  cashback, news.
- **Notifications backbone** — MailKit SMTP transport, dispatcher, outbox,
  contact fields + preferences; staff/owner password-reset backend. All three
  channels are proven live: SMS, Android push, and email — `no-reply@afk4.net`
  through the Stalwart server in Coolify since 2026-09-12, SPF/DKIM/DMARC pass,
  DMARC at `p=reject`, delivered outside spam (`docs/operations/email-delivery.md`).
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
  absent from the map. Since 2026-09-18 the picker also filters by city and,
  on request, sorts by distance — see the privacy note in the Player App Audit
  section below. Reviews are tied to a visit — one ended session, one
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

- **Off-peak pricing sells cheap hours as a separate tariff on a schedule** (revenue
  wave 2, slice 4) — a tariff now carries the days of the week and the local-time
  window it applies in, and a tariff cannot be chosen outside them. Windows *inside*
  a tariff were rejected on the evidence: a session is priced with one flat
  `TariffPricing` for its whole elapsed span, so windows would force splitting
  elapsed time across them and re-deciding whether the minimum billable duration and
  the rounding increment apply per window or per session — a rewrite of
  `TariffBilling`, which the live accrued-cost counter and the booking quote must
  agree with exactly. A scheduled tariff leaves that calculation untouched. No
  auto-selection was needed either: refusing a tariff outside its hours is enough,
  because then the morning tariff simply is not offered at eight in the evening.
  The gate sits at the two points where a tariff turns into money — session start
  (and extension) and booking pricing — and a booking is checked against **its own
  start time**, not against now, so an evening player can book tomorrow morning at
  the morning price. An already-running session stays bound to its tariff version to
  the end: the price never moves under a player, not even when the owner edits the
  schedule. The window may cross midnight and then belongs to the day it started on,
  so a club can sell «ночь с пятницы» without accidentally selling Saturday night
  too. Hours are shown, never hidden: a vanished «Утренний» reads as a broken app,
  while one labelled with its hours explains both itself and why it is unavailable.

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

## Honesty Wave (2026-09-16)

An API/UI audit ran across the whole tree and the owner froze every live run
(clean `manager_workstation`, physical gaming PC, staging money pass) until the
code of every part is finished. Everything below is code work done under that
rule, one PR each, all on `main` or open with green checks:

- **Sign-out revokes tokens** (#264). It cleared local storage only: a staff
  refresh token lived 30 days after "Выйти" on a shared club PC; the player app
  did the same. Both now revoke the presented pair server-side; a second
  workstation of the same person is untouched.
- **Named-pipe `Flush`** (#266). On a pipe `Flush` is `FlushFileBuffers` — it
  waits until the peer drains. The requester side burned its whole timeout
  inside a four-byte write (the agent reported "Organization Admin is not
  running" against a running app); the responder side needs it, because closing
  a pipe discards what the peer has not read. Split accordingly.
- **The «План» server tail is gone** (#265). The plan editor was removed on
  2026-06-17 by the owner's decision; the backend half (PUT, walls, seat
  coordinates, zone geometry, bulk contracts) lived on unreachable. Removed with
  a migration; the grid stays the only map view.
- **Twelve unreachable routes triaged** (#267, #269). Four were dead (legacy OTP
  sign-in, `GET /api/me/phone`, public branding by slug, duplicate `move-seat`);
  three were duplicates of live paths (tariff calculate, owner daily summary,
  rollout detail). Device enrollment codes were *wrongly* called dead by the
  audit — the staging smoke and the local-Postgres runbook provision machines
  with them; the server half stays.
- **Two real holes closed**: a player's top-up request was invisible at the
  counter — now a «Пополнения» queue in Касса, with online-paid requests
  deliberately not confirmable by hand (#270); and an invoice outside the
  subscription had no form at all (#271).
- **Loose reads typed** (#268): reservations, audit records and zone seats were
  still read as `Record<string, unknown>` — exactly where money and bookings
  are.
- **The seat menu stopped lying** (#272): six items (reboot, shutdown,
  wake-on-LAN, active window, fine, notify) looked live and only raised a toast.
- **Installer installs WebView2** (#273): every window a human sees on a club
  machine is WebView2, and a clean machine was met by an English "install it
  yourself" dialog.
- **Agent honesty** (#274): unknown commands answered "Accepted" (so the seat
  menu's six items were "executed" in the log); the server's credit-limit `warn`
  was thrown away, so a player learned about the debt from a dark screen; the
  launcher list was hardcoded empty against a working config; the installed-app
  inventory ran once per service start; reconciliation reported a constant zero
  pending events.
- **Workstation lock does something** (#275): it was two log lines and a
  `Task.CompletedTask` reported to the server as locked. It now disables Task
  Manager through machine policy and reports what it actually enforced.

Two defects in the above were caught by CI, not by local runs: a stale
`PUT /floor-map` call and four wrong device paths in `scripts/staging-smoke.py`
(the smoke had been failing those steps unnoticed), and the first, too-broad
version of the pipe `Flush` change, which hung the agent suite for 37 minutes.

## Player App Audit (2026-09-18)

Two passes over the Flutter player app (`src/afk4_customer_app`), same method as the
Organization Admin and Agent audits: first defects, then friction in real scenarios.
Twenty-one PRs (#337–#357), each stacked on the previous one; every check green at each
step. What the audit found clean: client and server agree on all 60 player routes (no dead
handlers, no calls to routes that do not exist), the localization catalogue is disciplined
(one technical literal outside `lib/l10n`), and money always goes through the shared
formatter.

**Pass one — defects.**

- **Requests had no timeout** (#337). A connection accepted and dropped — routine on club
  Wi-Fi — left "Покупаем…" on screen forever: the future never completed, so no `catch` ran.
  Bank-link failures and payment-status polls were fixed in the same PR.
- **Double-charge protection was a fiction** (#338, #344). The idempotency key was generated
  fresh on every tap, so it did nothing in the one case it exists for: a retry after a lost
  answer. `AttemptKey` now lives per attempt. Top-up intents and reservations had no key at
  all — not even in the contract; they now carry `IdempotencyKeyHash` on the entity itself,
  with a partial unique index per player (migration `20260918094040_AddPlayerAttemptIdempotency`).
  A repeated booking used to hold the money a second time.
- **A server hiccup signed the player out** (#339). Token refresh treated any non-200 — 500,
  502 from a proxy, 429 from the rate limiter — as a dead token and wiped the session.
- **Failures now name their reason** (#340, #341). `remainingAttempts` and `resendAfterSeconds`
  were dropped in the client; `tournament_already_registered` showed as a generic error and
  invited the player to pay the entry fee twice; extending a session called every 409
  "not enough money". Offline is now distinguished from a server refusal in eleven places.
- **Dead ends got a retry** (#342), bookings stopped being cancellable twice by a double tap
  (#343), and `PlayerDevice.Locale` was removed (#345) — it was written and never read, while
  its comment claimed push language came from it (it comes from the account).
- **Reads stopped growing with history** (#346). The club rating averaged every review row in
  memory on a public route; the bookings list returned every booking ever made and the app
  built all cards at once; package balances cost two queries per package.

**Pass two — friction.**

- Wallet re-reads its balance when the section is opened again and right after a top-up
  (#347) — it used to show the number read at app start, so an online payment left two
  different balances in two tabs. Online payment and a counter request no longer share the
  text "Заявка отправлена", and extending a session names the sum before the tap.
- **The club's answer to a booking reaches the player** (#348): `player.reservation_confirmed`
  and `player.reservation_rejected` did not exist, and the bookings screen was the only one
  that never re-read itself — the most anxious wait in the product was the only one with
  nothing to tell the player.
- First visit leads somewhere (#349), the ledger says what a charge was for and drops the
  bookkeeping vocabulary (#350, #355), a booking says how much money it holds (#351), the
  order button sits on the live-session card (#352), a booking takes two dialogs instead of
  four (#353), language and PIN come before they are needed (#354), and the club picker
  filters by city (#356) and by distance on request (#357).

**Privacy note on "near me" (#357).** Location is requested only when the player taps the
chip — never at startup — and only coarse (`ACCESS_COARSE_LOCATION`, `LocationAccuracy.low`).
The point lives in screen state: it is never stored and never sent to the server. `geolocator`
is the only new dependency and sits behind a `NearbyLocation` interface, so tests never touch
real geolocation.

**All of it is on `main` as of 2026-09-18**, merged bottom-up. Two mistakes are worth naming
because the CI caught both and the next stacked series will hit them again:

- Merging a child PR with `--delete-branch` closes the PRs stacked on top of it (GitHub closes
  a PR whose base branch disappears). #342 and #344 had to be reopened as #358 and #359. Retarget
  the whole stack at `main` first (`gh pr edit N --base main`), then merge without deleting.
- Two gates only exist in CI: `Verify generated contracts are current` (a C# record changed
  without re-running `bun run gen` in `packages/contracts`) and the Flutter end-to-end journey
  (`flutter test integration_test -d flutter-tester`, which a plain `flutter test` skips). Both
  fired on this stack; both are cheap to run locally before pushing.

**Deliberately not done, with reasons:**

- **Running balance in the wallet statement.** Reservation holds are hidden from the statement
  (correctly — they are a "−15/+15" pair with nothing in between) but they do count towards the
  wallet balance, so a balance computed from visible rows would not add up. Doing it honestly
  needs a product decision first: whether holds appear as rows.
- **`/api/me/achievements` still reads every session of the player's life.** The thresholds
  ("Veteran — 50 visits", total minutes played) are computed over the whole history, and
  "night owl" needs the hour in the club's timezone, which cannot be expressed in SQL without
  risking a query that does not translate on Postgres — and the in-memory test provider would
  not catch that. Needs aggregates or a stored counter.
- **`/api/public/organizations`** is still the heaviest response in the product: 50 clubs with
  all their branches, zones and photos, plus six unbounded helper queries. It grows with the
  platform, not with the player; reshaping the storefront response is its own task.

## Platform Control Audit (2026-09-19)

Two passes over the platform panel (`src/AFK4.PlatformControl.Web`), the last part of the
product without an audit of its own, same method as before: first classes of defect, then
friction in real scenarios. Eight PRs (#361–#368), stacked; every check green at each step.
What the audit found clean: almost every mutating button was already guarded against a
double click (one exception, "Sign out"), error text for *actions* already went through
`describeApiError` with server error codes parsed by name, and money everywhere goes
through the shared formatter.

**Pass one — defects.**

- **No request had a deadline** (#361). Not one route. A silent server left the tab on
  "Saving…" until the browser gave up on its own, which takes minutes. Twenty seconds now,
  and a timeout is told apart from an unreachable server: the first means the request left
  and may have been carried out.
- **A retry created a second one** (#361). `sendIdempotent` minted a fresh key per call, so
  it only ever protected against a double click — not against the case it exists for, a
  retry after a lost answer. `useAttemptKey` keeps the key for the attempt and ties it to
  the request body, so a corrected amount is a new attempt (the server rejects the same key
  with a different body). Creating a club went through `send` entirely, although the server
  has held `platform.organizations.create` idempotency all along.
- **A refresh hiccup signed the admin out** (#361). Any non-OK answer to `/auth/refresh` —
  500, 502, a dropped connection — wiped the session; the admin paid for someone else's
  outage with a fresh sign-in and a code from their phone.
- **"Не удалось загрузить данные" stood on twenty-odd screens** (#362) and meant anything:
  no permission, no network, server down. Ten near-identical loader hooks put the
  transport's English technical string into state, and the screens did not even read it.
  One `useLoadable` now owns loading, and the reason reaches the eye; the specific "what did
  not load" stays as the heading next to it.
- **Five places swallowed failures silently** (#363). The client passport's price and next
  invoice stayed a skeleton forever, its owner row quietly became "—" (which means "no
  owner"), and its main button opened nothing because the subscription was never in hand.
  The subscription dialog left one plan in the list without a word. The mail check painted a
  red "не ушло" on a request that never reached the server — a claim about mail nobody had
  tested. Global search had no way back from an error.
- **Server codes were on screen where words belonged** (#364): the journal showed
  `OrganizationOwnerInvite`, `Denied`, `PlatformApi`; the passport printed raw `stable`; the
  account menu listed machine permission keys that already have translations in the roles
  section; chart tooltips were labelled `recurring`/`oneOff`. The *action* in the journal
  stays machine-readable on purpose — about two hundred of them, they match what goes to the
  logs, and half-translating is worse than an honest code.
- **The fleet pulse read whole tables to produce a few dozen numbers** (#365): every device,
  every seat, every open session of the network came into memory to be counted. The database
  counts now; the number of queries is unchanged.

**Pass two — friction.**

- **The journal answered "who touched this club" with a column of GUIDs** (#366), and
  filtering by club meant fetching the id from the club's URL. The record now carries the
  club's name, the club filter is a list, and the outcome filter is three words instead of
  free-typed English.
- **Duty screens showed a snapshot without saying so** (#367). The fleet overview is called
  "Now", is kept open all day, and never refreshed. It re-reads itself every minute now
  (health every two), both say what moment they show, and a background refresh neither
  shows a spinner nor blanks the screen. A hidden tab polls nothing.
- **The owner's access code could only be retyped** (#368). Inviting a platform teammate was
  done properly — warning, activation link, copy buttons — while the club owner's code, the
  same act performed more often, was a bare string in a table cell. Both now share one
  block; a failed clipboard write no longer passes silently.

**Deliberately not done, with reasons.**

- **Rollouts still go to every club at once.** Publishing a package creates a 100% rollout
  across the whole network, and the code says this was chosen on purpose ("ceremony costs
  more than it buys at this scale"), guarded by a test that spells out the reason. The
  server supports waves (`BatchPercent` with stable per-device bucketing) and the panel
  could offer them, but reversing a deliberate product decision is the owner's call — and a
  wave that nobody widens leaves half the fleet on an old version silently, because there is
  no rollout progress view at all. Waves and progress belong together, as one decision.
- **Action names in the audit journal stay machine-readable** (see above).
- **The health screen still shows raw template keys** for failed deliveries. That screen is
  read by a technical role, and the key is how the template file is found; translating it
  would cost the link to the artefact.
- **Platform lists remain unpaged** — the known debt: organizations, invoices, subscriptions
  and the audit search all load in one go (audit caps at 100 rows). It holds at tens of
  clubs and needs revisiting at hundreds.

## Third Pass — Design (2026-09-22)

The audit method grew a third pass: after defects (pass 1) and friction in real scenarios
(pass 2), a pass that asks whether the part *reads* as a considered thing. It ran over every
part that has an interface, plus the shared layer underneath them. Twelve PRs (#371–#382);
every one green on CI before it was offered for review.

Deliberately skipped: the **Player Shell** (owner's decision — it is rewritten from scratch
last, polishing it now is paid-for twice), and the **agent service / Platform API**, which
have no interface for this pass to look at beyond words that reach a human.

**Shared layer first**, because a class fixed there is fixed everywhere:
- #371 — micro-states existed on loud controls and were missing on quiet ones: a field in the
  console did not react to the cursor although the same field on the sign-in screen did, tabs
  did not confirm a press, the empty-state button was drawn around the kit entirely. A guard
  now fails the build when an element has a hover state but no focus state.
- #372 — sixty-one rules referenced tokens that do not exist (`--text-muted`, `--border-subtle`,
  `--surface-panel` and eight more). An invalid `var()` drops the whole declaration silently,
  so the Reports section had been rendering with no table borders and no panel backgrounds.
  The guard that should have caught it was reading `styles.css`, a barrel of `@import` lines —
  it checked nothing and was always green. It now walks every stylesheet of all three web apps.
- #373 — both dialog wrappers (29 modals in Organization Admin, 33 in Platform Control)
  announced themselves correctly to a screen reader but never managed focus: Tab kept walking
  the page underneath the modal, and closing threw focus back to the top of the document.
- #374 — `<html lang>` never followed the chosen locale; the platform panel carried `lang="en"`
  while its interface was Russian, so a screen reader read Russian text in an English voice.

**Platform Control:**
- #375 — a permission refusal arrived in the same red block as a crashed server, with the same
  Retry button. The button promises what will not happen; the only real action (ask for access)
  was not on screen. `retryCanHelp` now travels next to the reason through `useLoadable`.
- #377 — the screen blinked a skeleton after its own successful action ("Mark as paid", "Lift
  suspension"): the content a person was reading vanished for half a second. A refresh on top
  of shown data is quiet now, and the skeleton waits 180 ms before appearing at all — almost
  every answer arrives faster, and every one of those used to flash.

**Organization Admin:**
- #379 — four booking states in the timeline differed by fill colour only, with nothing but the
  guest's name inside the block: no word, no glyph, no legend, and nothing at all for a screen
  reader. Same class in the POS order ticker.
- #380 — the copy glossary lists sixteen rules and the guard test enforced three. The rest
  lived in the memory of whoever wrote the string, and did not survive: the ledger badge said
  «сторно» while the player's own statement calls the same event «Отмена операции». The guard
  now enforces nine rules and immediately found a tenth violation (a «Дашборд» menu item).

**Player app:**
- #378 — eight screens drew their own "error text plus Retry": different buttons, different
  padding, different alignment for one and the same state. Seven are now the shared
  `LoadFailure`; the eighth is a deliberate partial failure and stayed. Loss of connection is
  now told apart from a real failure — a player reads "could not load" as the club being broken
  and calls support, when the cause is their own internet. Writing that test exposed a real
  hole: `search()` never wrapped its parsing, so malformed JSON escaped as a raw
  `FormatException` that no screen catches, and the club storefront — the only way into the
  app — crashed instead of offering a retry.
- #381 — the profile screen replaced itself with a spinner while re-reading data it already had.

**Setup wizard:**
- #382 — the hint under the new-PIN field was called without parameters while the catalog
  string is `«{min} цифр.»`: ICU fails and returns the raw text, so a person installing a club
  literally saw `{min} цифр.` on screen. The neighbouring "6 digits" hint was typed by hand
  past `PIN_LENGTH`. The sign-in button went dead below six digits without saying so. The hall
  screen refused to create seats when the branch had no hall and never said why — the very same
  case is explained in words on the device screen.
- #386 — every screen remounts on its step, and what was typed lived inside the screen: "Back"
  met a person with defaults even where they had already sent something. The invited staff and
  their codes vanished (the only copy if the SMS never arrives), the tariff offered itself for
  creation again — and the server refuses the same tariff twice. Six screens, not four: device
  name and the sign-in number had the same hole. Drafts now live in `App`, in memory only; the
  PIN is deliberately not brought back, and branch-owned drafts do not follow a branch change.
- #387 — six fields on the staff, hall and tariff screens lay outside any form, so Enter did
  nothing, while sign-in and the device screen right next to them submit on Enter. They now
  follow that pattern; a disabled button and a request in flight both swallow Enter.

**Along the way:** #376 — `WorkerTests.RotationRequest_...` waited for a ten-second heartbeat
inside a twenty-second budget and fell over on a loaded runner; it painted the CSS-token PR red.
The fake server now announces a one-second interval — this test never checked the pause itself.

### Named, not done

Closed on 2026-09-23 (PR #385–#406, all merged): empty states now carry a required decision
about the next step in both panels (`next` in Platform Control, `EmptyState` in Organization
Admin — `tsc` refuses a list without one); a disabled control on the cashier's paths names its
reason (`useBlockedReason`: seat menu, session start, booking start, top-up, POS client picker,
receiving, device assignment, staff PIN reset); the player app takes colour from the theme, has
48dp targets, a reachable light theme (dark by default) and a 1.3 font-scale ceiling; the setup
wizard keeps input on Back, submits on Enter, weighs its actions and calls the program one name;
«Повторить» appears only where a retry can help. Names changed by the owner the same day: the
club program is «Панель AFK4.net» (installer, shortcut, window, catalog), the person at the
counter is «администратор», never «оператор» — both guarded in `voice.test.ts`.

Closed on 2026-09-23 afternoon (#411–#415): management screens say «только просмотр» and who may
change; Platform Control and the remaining pass-through `disabled` props name their reason (most
turned out to be in-flight or self-evident; the booking drawer was the real gap); the status block
on the «Лимиты» tab needs the status right; every loading screen in both panels has a skeleton of
its final shape (`ManagementScreen` refuses `state` without `skeleton`), shown after 180 ms.

Closed on 2026-09-23 evening (#417–#429):
- **No active branch** (#417) — the shell says it once, on one screen, instead of grey forms; an
  enrolment bound to a foreign branch no longer becomes the «active» one.
- **Loading, the rest** (#422, #428, #429) — shaped skeletons in cash, news, client packages and
  support access; a new report period refreshes quietly over shown data; «События» names a list
  failure instead of waiting forever; the cash tabs reread after the person's own action without
  blanking the screen (`useShownFor`).
- **Words** (#420, #426) — the wizard has one name; Platform Control says «организация»; tg keeps
  one word per concept (guarded, `TG_ONE_WORD`), the shift supervisor is «сардори навбат»; the
  gaming PC is «ПК» in every language — «машина», «мошин», computer and machine are guarded too.
- **Player app** (#419) — the club accent is checked for 4.5:1 contrast in both themes.
- **Client card** (#423) — «Посадить за ПК» and booking from the card compared raw seat states
  with lowercase literals the server never sends, so on production they offered no seat at all.
- Also: the command palette finds bar orders (#421); owner reports proven on real PostgreSQL and
  the report plan closed (#418); a test's `mock.module` must be put back in `afterAll` (#424,
  guarded — it caught one more leak on the way in); a quiet-refresh race in Platform Control's
  `useLoadable` flashed a skeleton when two timer ticks came back to back (#427).

Closed on 2026-09-23 night (#425, #431–#436), after the owner's answers:
- **Booking move** (#425) — seats are offered for the booking's own window; a session without an
  end blocks a seat only once that window has started.
- **One money rule in reports** (#434) — money counts on the day it moved, net of refunds, on
  every report screen: bar payments and refunds on their own days, gameplay charges and refunds
  from the session ledger. The day's total now equals its trend point and the per-cashier sum.
  Play time stays session-based; the sales list stays a list of sales.
- **«Кэшбек»** (#431) — one Russian spelling, guarded; Tajik keeps «кэшбэк» by its glossary.
- **Seat states in the contract** (#435) — `SeatStateNames`; the contract generator now emits all
  57 `*Names` dictionaries to TS, and a field whose comment names a dictionary gets its value type
  (`SeatStatusDto.state: SeatStateName`), so comparing with `'free'` fails `tsc`.
- **Client card** (#436) — packages, package sale and the seat select are styled; the
  «Комплиментарная сессия» checkbox is inline again.
- **«Посадить за ПК» from the card** (#432) — five tariff requests per open became one.
- **Web tests hanging for minutes under load** (#433) — a failed element assertion made bun print
  the DOM node with its whole document (2.1 MB for a bare button). Nodes now print as short HTML.

Closed right after (#438–#440):
- **Staff across branches** (#439) — the owner adds a person from the network to a branch, removes
  them from a branch, and returns someone left with no branch; saving roles no longer strips an
  owner's own role in that branch; a dangerous-action confirmation scrolls into view and focuses
  «Отмена» (it rendered below the fold in list-and-card screens — all 12 uses).
- **Wizard WPF windows** (#438) say «мастер установки», guarded by `voice.test.ts`.
- **Dart dictionaries** (#440) — every `*Names` dictionary is a constants class in
  `contracts.dart`; the player app compares reservation, tournament, bar-order states and club
  features through them.

Still open, named:
- **Looking with eyes** — the passes read markup, styles and states rather than running the
  product: the live stand is frozen by decision.

**Closed after the pass — partial failure swallowed silently.** The pass counted 8 + 2 by
reading; walking every screen loader found 10 in Organization Admin and 3 in Platform Control.
Either one refusal wiped the half that had arrived (a staff-list 403 blanked Halls, Tariffs,
Staff and Goods at once; a missing invoice list hid the subscription), or the secondary request
fell into a silent fallback that told the person something false (an unknown shift read as
«open a shift», a failed device list as «no devices», News hung in loading forever). Each section
now keeps what arrived, names its own reason and retries only itself (`useLoadable` with
`retryCanHelp` in Platform Control; `PartialLoadFailure` / `SectionState` over
`projectOperatorError` in Organization Admin). Left all-or-nothing on purpose: the POS (a sale
needs catalog, categories and shift together), the club profile form (one save writes both
halves), and the current shift in the cash cockpit (without it the screen cannot offer open or
close). Still silent by design and not touched: the players' live «now» column, branch rollup
KPIs, the shift-close tolerance lookup.

## Latest Verification

- Cleanup and gates round (2026-09-02…03, PRs #207–#212). Three dead stacks
  removed: the switched-off WPF Organization Admin (8884 lines + 23 test files),
  the React player web, and the unused `AFK4.BuildingBlocks` project — about
  24 000 lines and one styling stack. **The release path was broken and nobody
  saw it:** `scripts/register-update-package-requests.ps1` still posted to the
  club-scoped update routes that moved to the platform level, so `Package Smoke`
  had been red on every push to `main` since 2026-08-21. The script now speaks
  the platform contract; package registration was taken out of the smoke run
  entirely, because that route needs a platform-admin session and that session is
  behind two-factor — a bypass token for CI was refused. Package Smoke is green
  again (first green since 2026-08-21) and staging deploys.
  Removing the dead stacks broke the staging deploy once: three Dockerfiles still
  copied the deleted paths, and `deploy/**` was in no PR path filter. Both fixed —
  the directory now triggers checks and a test asserts every `COPY` source
  exists.
  Added in the same round: in-app push handling in the player app (a notification
  now opens the screen it is about; a test caught that the in-app banner sat
  under the bottom navigation and its button hit «Профиль» instead), a narrow
  Biome lint gate on the web workspaces (it caught a shadowed `escape` that would
  have URL-encoded the audit CSV export), 27 tests for the setup-wizard host
  bridge (moved into `SetupWizard.Core` so it is testable off Windows), and 16
  tests for the dashboard summary calculation.

Older verification entries (2026-07-28 and earlier, including the superseded
Platform Control rebuild Tasks 1-7 gates) are archived in
`docs/archive/progress/2026-08-06-vertical-slice-detailed-history.md`.

- Cleanup round (2026-08-17, after the review-fix round below): three leftovers
  closed. **Postpaid billed the same minutes twice.** A fixed-duration postpaid
  session records a debt for its minutes at start and at every extension, and
  checkout then priced the whole elapsed span again: the guest paid the full
  amount at the till and walked out still owing it on the ledger, because the
  fresh debt and its settlement netted to zero while the start debt stayed
  untouched. Checkout now records only what is not already on the session and
  settles the whole outstanding balance. The same change fixes the repricing that
  the tariff-schedule work made likely: postpaid debt entries now carry their
  billable seconds, so already-billed minutes keep the price they were sold at,
  and the tariff in force at checkout applies only to the overstay. Two tests,
  both mutation-checked: without the fix one leaves 2250 of phantom debt, the
  other demands 3750 at the till where 2300 was owed. Open tabs bill exactly as
  before — they have nothing recorded to subtract. **The localhost CORS origins
  are no longer appended in production** (`CorsOrigins.Resolve`); staging and dev
  keep them, so the browser verification route still works, and a production
  deployment lists its own origins or gets a startup warning. **444 lines of dead
  CSS** removed: `15-settings.css` kept the whole pre-`payset-*` payments and
  loyalty stylesheet plus the retired settings shell — 46 class names with no
  reference anywhere in the repository. Full suite green, including all 28
  PostgreSQL tests (2134 backend, 1108 web).
- Review-fix round (2026-08-17): three independent reviewers went over the two
  merged revenue slices and this branch's docs/cleanup. They found real defects in
  merged code, all now fixed and each pinned by a test that was mutation-checked
  against the unfixed code. **A scheduled tariff was only checked at the start
  instant**, so a booking 08:00–23:00 on an 08:00–16:00 tariff was priced entirely
  at the morning rate; the gate now requires the tariff to apply throughout the
  whole span, and falls back to the instant only when there is no known end (an
  open-ended session). **The capacity check and the booking insert were not one
  transaction**, so two concurrent bookings for a one-machine hall both committed
  with two wallet holds; both online paths now run Serializable with a retry, the
  same pattern session start already used, and a real-PostgreSQL test with a read
  barrier reproduces the double booking when the transaction is removed.
  **Seated reservations were dropped from occupancy** on the false premise that a
  session holds their seat — `SeatAsync` creates no session — so a physically full
  hall could read as empty; seated now occupies, and a session with no scheduled
  end (or one already passed) occupies through the present moment instead of
  vanishing. **The day-of-week toggles inverted their own meaning**: an everyday
  tariff shows all seven days selected, and the first click XOR-ed against zero,
  turning "not Saturdays" into "Saturdays only". **`PATCH /tariffs/{id}` erased the
  schedule** whenever a caller omitted the fields; the schedule moved into a
  nested object where absent means keep. **Guest and comp sessions bypassed the
  gate** — a guest session carries a tariff version that checkout prices from, and
  a comp valuation feeds the manager-approval threshold; both are gated now.
  Docs: the pilot runbook sent readers to a `Pilot Setup` panel that no longer
  exists and to a staff step the UI cannot finish (an invite has no redemption
  screen anywhere in the repo), the roadmap still advertised that panel, the
  permission names had lost their `organization.` prefix, the CORS caveat
  overstated the risk (auth is bearer, not cookie), and a `.claude/memory` note
  still called the deleted settings sections live.

- Off-peak pricing gate (2026-08-17): Platform API passed **2105 tests against a
  real PostgreSQL database with zero skips**; Shared Contracts 141/141;
  Localization 15/15; Building Blocks 3/3; Update Publisher 13/13; `@afk4/i18n`
  39/39; Organization Admin Web 1106/1106 plus its production build; Platform
  Control 286/286; the customer app passed 323 widget tests with a clean
  `flutter analyze`. Read that PostgreSQL number carefully: it is true of the
  suite, but `PlatformApiFactory` runs on the in-memory provider, so the tests for
  a feature built on it never execute their SQL against Npgsql and can observe no
  isolation behaviour at all. The nine gate tests were re-run with the schedule check
  neutered: exactly the four refusal cases fail and the five "must still work" cases
  pass. Fourteen more unit tests pin the window itself — midnight crossing, day
  masks, half-open boundaries and the branch time zone — with a fixed-offset zone
  rather than a named one, so the answer cannot differ between Linux and Windows.
  The migration `AddTariffSchedule` adds three nullable/defaulted columns to
  `tariffs`, so every existing tariff keeps applying round the clock. The
  full-solution build was not run: the Windows-targeted projects cannot build on
  Linux. Not exercised on a device or against staging.

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

- **The PC does not use its own sign-in yet.** Since P2a (2026-09-24,
  `docs/superpowers/plans/2026-09-24-shell-p2-server.md`) the server lets a player
  sign in on a gaming PC through the agent (`/api/devices/{id}/player-sign-in`):
  attempts are counted per machine, a player never gets into someone else's
  session, and the tokens are bound to the PC and revoked by the server — five
  minutes after a sign-in that never started a session, thirty seconds after the
  session ends, and at once on a new sign-in, a seat move, removal or a forced key
  rotation. The heartbeat now carries the seat, the session owner and the club's
  features. Since P2b a player can also sign in by scanning the PC's QR with the
  app (`/api/me/devices/sign-in-claims`, redeemed by the PC with its key); the
  seating code is single-use, wrong codes are counted per player and per club,
  and a self-start repeated with the same key returns its session instead of
  "code invalid". Since P2c the choose-time screen gets its prices in one call
  (`start-offers`, `extend-offers`, `end-quote`, all priced by `TariffBilling`), a
  player can start from their own package, and an early exit no longer charges
  the admin's pause or burns the unplayed package minutes. Since P2d the server
  knows the device commands (reboot, shutdown, wake through a neighbour in the
  same subnet, sign-out, message, maintenance on/off, policy refresh), refuses
  unknown types, keeps power and maintenance away from a running session, and
  hands a reboot, shutdown or wake to the agent once and never after ten minutes.
  Since P2e every early end returns the unplayed prepaid time and package
  minutes — the counter and auto-protection as well as the player (owner,
  2026-09-24) — inside the same transaction that ends the session, which also
  closes a double refund on two near-simultaneous player exits.
  The agent does not execute the new commands yet (it answers "not implemented")
  and does not report its MAC; that lands after P1. The shell host still signs
  in through the public route with unbound tokens and does not pick up claims;
  P3 moves it onto the agent.

- **Rendered Reports QA** — the redesigned Organization Admin Reports views
  have automated component/App coverage and a green production build, but still
  need a native WebView2 visual pass at 100%/125% scaling in dark and light
  themes together with the broader clean `manager_workstation` smoke below.

- **The gaming PC still has no OS-level kiosk.** Lock now disables Task Manager
  through machine policy and reports what it enforced (#275), the launcher list
  and the credit-limit warning reach the player, unknown commands are refused
  and the app inventory runs on a schedule (#274). What is still missing is the
  half that cannot live in a service: swallowing Win/Alt+Tab and holding the
  shell in front need a hook on the interactive desktop, and the agent sits in
  session 0. So a player can still Alt+Tab out of a "locked" PC — the difference
  is that the server no longer claims otherwise. That half belongs to the Player
  Shell rewrite (plan P5). Plan P1 of the rewrite (2026-09-24,
  `docs/superpowers/plans/2026-09-24-shell-p1-agent-truth.md`) made the agent
  tell the shell the truth: one persistent pipe `afk4-shell-v2` with an ACL
  instead of two open per-message pipes, state pushed on change instead of
  polled, `offline` and `ending` actually produced, `IsOnline` from the last
  contact instead of a constant, the seating code hidden when offline or
  expired, grace no longer called a credit limit, games refused outside a
  session and started in the player's session instead of session 0. «Позвать
  оператора» now reaches the counter through the agent; the shell's «пауза» is
  gone — pause stays the admin's (#279). Plan P4a replaced the shell's web UI with
  a new foundation: the `player` token theme under the contrast gate, bundled
  Golos Text and JetBrains Mono, all text in `locales/*` under `playerShell.*`,
  the screen chosen from the agent state by one function, a practice host for
  the browser (`?scenario=`), and the idle, offline, maintenance, error and
  basic session screens; sign-in, choose-time and the full session follow in
  P4b–P4d. The new UI speaks bridge v2 and must ship with the P3 host. **The
  agent and the shell must be updated together:** the old and the new pipe do not talk, and until the first
  club this is cheaper than a compatibility bridge. Not proven by any automated
  check: the pipe ACL against a real standard user and a game appearing on the
  player's screen — that is P5 acceptance on a live PC. Clock drift is still
  detected and only logged — a deliberate deferral until real fleets show
  whether they drift.

- **Release registration needs a human.** Registering an update package is a
  platform-admin action behind two-factor, so CI cannot do it: `Package Smoke`
  builds, signs and publishes artifacts and verifies the stable installer, then
  prints what is waiting to be registered in Platform Control. A machine
  credential scoped to `platform.updates.packages.manage` would give CI that step
  back — an open policy decision, recorded in
  `docs/operations/update-package-publishing.md`.

- **iOS side of the mobile app** — no APNs key, no iOS build, no `ios` folder at
  all. Android is verified end to end, and since 2026-09-03 the app also handles
  an incoming notification: tapping one opens the screen it is about. iOS is
  untouched.
- **Capacity is checked by machine count, so two cases stay open by design.** A
  branch with no attached, approved gaming PC is treated as unlimited — an
  unconfigured branch should not explain its own misconfiguration to a player. And a
  session with no scheduled end (`EndsAtUtc == null`, the ordinary walk-in) is not
  projected forward: a booking an hour out can be accepted next to one. Counting
  open-ended sessions as occupying the future would refuse tomorrow's bookings
  because the hall is full today, which is worse. Counter-side bookings
  (`CreateAsync`) are deliberately not capacity-checked: the operator sees the floor
  and may overbook on purpose.
- **A tariff with hours is checked at the start moment only — owner's decision,
  2026-09-23.** A session or booking that starts inside the tariff's hours is
  billed at that tariff to the end, and extensions are not checked against the
  schedule at all: a walk-in who sat at 15:30 on 08:00–16:00 pays the morning
  price all night. The alternative (repricing or refusing past the window) left
  clubs explaining why two identical sessions cost differently and why "open"
  was allowed while "two hours" was not. A club that wants a separate evening
  price adds an evening tariff or a package with its own window. The tariff
  editor says this next to the hours.
- **Operator entity search** finds seats, players, reservations and receipts
  and opens each one in place (#280), within the finder's own rights. Bar orders
  are the one kind it does not search.
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

Platform Control was audited on 2026-09-19 (#361–#368), which closes the last
part that had none. The order is the owner's, set on 2026-09-16: **every live run
is frozen** — the
clean `manager_workstation` pass, the physical gaming-PC smoke and the staging
money pass all wait until the code of every part is finished and satisfies the
owner. The Player Shell is last of all, and its current implementation is to be
thrown away rather than polished.

1. **The Player Shell, rewritten — started 2026-09-24.** The owner decided: a
   player signs in on the PC itself (phone and PIN, or QR from the app) and
   starts from the wallet; the shell replaces explorer for a dedicated player
   account; the free PC is a club showcase, and on the free plan it also shows
   platform ads; the session ends with a visit rating and optional tips. The
   player does not pause themselves (owner, 2026-09-23). Scope and order are in
   `docs/roadmap/production-readiness.md` → «Launch Scope»; requirements in the
   PRD §6. Reading the current code found holes the rewrite has to close: the
   next player inherits the previous sign-in (nothing signs out on lock, tokens
   are never revoked); games are likely started from the service in session 0;
   the command pipe has no ACL for a standard user; `IsOnline` is hardcoded and
   `offline`/`ending`/`maintenance` are never produced; the shell's API address
   is set by nobody, so it would call production from staging. None of this was
   seen on a PC — reading only.
2. **Pricing was never set in somoni.** The seeded Starter price of 2 900 TJS
   was a ruble figure relabelled; the owner set free up to 10 PCs, then 10 TJS
   per PC (billing spec §6a). Until the plans are reworked, do not show the old
   prices to a club.
3. **Rollouts in waves, with a progress view — deferred by the owner (2026-09-23).**
   There are no clubs yet, so a package still reaches everyone at once, on purpose
   and guarded by a test. Before the first clubs, decide waves together with a view
   of how a rollout is going: device-level counts exist in `DeviceUpdateStatuses`,
   but no endpoint exposes them, and a wave nobody widens leaves part of the fleet
   behind silently.
4. **Pre-production decisions** in `docs/roadmap/production-readiness.md`:
   Authenticode custody, production object store/CDN, package-registration
   credentials, backup encryption/retention/ownership, incident and rollback
   checklist.
5. **iOS does not ship yet** (owner, 2026-09-23) — no Apple account, no APNs
   key, no `ios` folder; revisit before launch.
6. **Then, and only then, the frozen evidence**: the live revenue-wave pass, the clean
   `manager_workstation` pass at 100%/125%, and the physical Windows gaming-PC
   smoke.

Known smaller debts worth picking up between the big pieces: `/api/me/achievements` reading the
whole visit history and the weight of `/api/public/organizations` — both deferred until the first
club. Closed on 2026-09-23: the `mock.module` leak (#424), the report-plan tail (#418), the running
balance and hold lines in the wallet statement (#396).
