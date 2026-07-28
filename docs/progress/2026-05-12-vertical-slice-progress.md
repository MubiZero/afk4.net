# AFK4 Current Progress Snapshot

Last updated: 2026-07-28

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

Plus the earlier base: identity/tenancy/RBAC/audit, devices/floor-map, owner-code
enroll, session lifecycle + leases, ledger/POS/shifts/reports, update publishing
+ rollout, and the Agent/Setup-Wizard/Player-Shell/packaging stack.

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

## Latest Verification

- Platform/organization big-bang RC gate through code SHA `6e9a8507` (2026-07-28):
  `dotnet build AFK4.sln -c Release -p:EnableWindowsTargeting=true` completed
  with 0 warnings and 0 errors. Platform API passed 1451 tests with 14 explicit
  PostgreSQL-environment skips; Shared Contracts passed 131/131; the remaining
  portable .NET suites passed 196 tests with one environment skip. Platform
  Control passed 119/119 plus its production build; Organization Admin passed
  1018 tests with 26 explicit skips plus its production build; i18n passed
  39/39; Setup Wizard Web passed 20/20 and its bundled WebAssets were rebuilt.
  The Platform Control Docker RC image built and returned `ok` from `/healthz`.
  The current-product vocabulary guard passed via its native ripgrep-equivalent
  check. Native Windows verification from a temporary `C:` worktree passed
  Organization Admin 238/238, Player Shell 33/33, and Agent/release automation
  185/185. A real unsigned package smoke produced the master
  `afk4-client-0.1.999-internal.exe` plus the Organization Admin, Agent, and
  Player Shell MSI inputs. This RC verification preceded the staging cutover
  recorded below; no production deployment, installation, or signing was
  performed.

- Staging platform/organization big-bang cutover completed for release SHA
  `1123a53f` on 2026-07-28. The verified pre-cutover custom-format backup is
  `/root/afk4-cutover-backups/afk4-pre-bigbang-20260728T1800Z.dump` with SHA-256
  `141b40fa4c61726fd54bae7414104d6e2dd4d59e975dd5fe6cc5bc1156057ea3`;
  `pg_restore --list` passed. The database advanced through
  `20260728170032_NamePlatformOrganizations`; unknown organization/platform
  roles and active interactive tokens are zero, while 18 device credentials
  remain active. Coolify deployments `u2pnhb9xkkabeqo2tnip9ndn` (Platform API)
  and `v100hamkph0fof3c9pgonumr` (Platform Control) finished from that SHA with
  image IDs `sha256:b5c8341c8a30d1001e2540d4bab4f9bd64f5772c85008f877e6101507bb0bb2b`
  and `sha256:5044140553ceb4045087591ab4f73b0b3686a2db86ce47d3eed0e7e929450d33`.
  Public API and Platform Control health checks returned 200; a legacy
  Organization Admin request returned the required 426, the current epoch
  reached authentication and returned 401 without credentials, and the old
  club route returned 404. GitHub staging deploy run `30386318369` and Package
  Smoke run `30386102803` passed. The obsolete staging club web application was
  stopped and its public route returns 503. Production was not touched.

- Fresh Platform Control removal gate (2026-07-28): the remaining Platform Control
  suite passed 119/119 tests across 50 files; focused routing/onboarding passed
  7/7, including explicit rejection of `/club/*` and staff sign-in routes while
  preserving stateless owner-invite onboarding; i18n passed 39/39 and the
  production build completed. The corrected Coolify Dockerfile built the nginx
  image and `/healthz` returned `ok`. Existing dialog accessibility, React
  `act`, and large-chunk diagnostics remain non-failing.

- Fresh Operator parity gate (2026-07-28): full Organization Admin Web suite passed
  1018 tests with 26 explicit skips and 0 failures across 162 files; App
  integration passed 68 with 26 skips and 0 failures; i18n passed 39/39; the
  Organization Admin Web production build completed. Existing React `act` diagnostics,
  SignalR annotation warnings, and the large-chunk warning remain non-failing.

- Fresh authoritative-footer gate: Shared Contracts passed 129/129; Platform
  API passed 1391 with 13 PostgreSQL-environment skips; Organization Admin Web passed
  708/708 component/model tests plus 89/89 App integration tests; i18n passed
  35/35; and Platform Control passed 381/381. Platform Control and Organization Admin Web
  production builds completed, and `dotnet build AFK4.sln
  -p:EnableWindowsTargeting=true -p:NuGetAudit=false` passed with 0 warnings and
  0 errors. The Windows-targeted Organization Admin and test assembly compile on
  Linux, but the testhost cannot execute there because
  `Microsoft.WindowsDesktop.App` is unavailable; run that suite on Windows
  before integration.
- Chrome/Chromium rendered footer QA passed at dark 1920, dark 1280, and light
  1280 with a measured 32 px row, no wrapping or overflow, and zero console,
  page, or request errors. The supplied-reference comparison and resolved
  findings are in `design-qa.md`.
- `dotnet restore AFK4.sln -p:EnableWindowsTargeting=true -p:NuGetAudit=false`
  and the matching full solution build passed with 0 warnings and 0 errors.
- Shared contracts passed 129/129. The complete Platform API suite passed
  1401/1401 with no skips against isolated commerce, POS, and reservation schemas
  on PostgreSQL 17.10, including deterministic settlement/refund, inventory/
  currency, reservation-start, rollback, and cross-command concurrency tests.
  The full solution build passed with 0 warnings and 0 errors.
- Organization Admin Web passed 797 tests across the component/model and App integration
  runs;
  the generated ru/en/tg catalog check passed 23/23 and the production build
  completed. Rendered cash-terminal and system-footer QA passed in dark and
  light themes at 1920,
  1440, 1280, and 1100 widths with no browser console errors; the selected shift
  design comparison is recorded in `design-qa.md`. Existing React test diagnostics,
  test-harness `ECONNREFUSED`, SignalR annotation warnings, and the large-chunk
  warning remain non-failing.
- Platform Control passed 381/381 Bun tests and its production build; its existing
  large-chunk warning remains.
- Player Shell Web passed 51/51 Bun tests and its production build.
- GitHub `Package Smoke` run `29326881732` and the migration-gated `Coolify
  Staging Deploy` run `29327547027` passed for merge commit `498b7b83`. The
  staging database contains all 69 migrations through
  `20260714130000_VersionReservationsAndLinkSessions`, and the deployed API
  container plus public `/api/health` check are healthy.
- P0 fixes passed latest-head Windows PR Verification in runs `29334853943` and
  `29336317700`, merged through PRs `#124` and `#125`, and deployed from merge
  commits `2922dbd2` and `99f43338`. Coolify staging deploy runs `29335387633`
  and `29337673756` completed with public health verification; the second push
  deploy used one builder and completed without the transient duplicate-worker
  collision seen on the first push attempt.
- The Linux full-solution test attempt cannot directly run WindowsDesktop or
  Windows PowerShell tests, but the same WSL host can invoke the installed
  Windows runtime. The current Windows results are recorded in the big-bang RC
  gate above; release automation must use a drive-letter worktree because nested
  Windows PowerShell refuses `-File` scripts from a WSL UNC path.

## Known Gaps

- **Per-environment SMTP config** still needs the user's real connection
  details wired into `NotificationOptions`.
- **Operator entity search** is still deferred: the command palette navigates
  between workspaces but does not yet search clients, seats, reservations,
  orders, or receipts.
- **Remaining Windows evidence** is narrower: repeat the Operator pass on a clean
  `manager_workstation` install at 100%/125% scaling and run the physical Windows
  10/11 gaming-PC smoke for lock/unlock enforcement, reboot recovery, and
  role-aware update/rollback. The WindowsDesktop and unsigned package-build gates
  are already green; this remaining item is physical-device UX/runtime evidence.
- **Rotate the former staging smoke credential** before the next staging smoke;
  it was removed from the tracked runbook and must be supplied only through the
  approved secret store.
- **Pre-production release decisions** remain: Authenticode custody, production
  object store/CDN, presigned upload automation, package-registration
  credentials, staging secret rotation, backup/restore ownership — tracked in
  `docs/roadmap/production-readiness.md`.

## Recommended Next Work

1. Repeat the now-passing Operator day flow from a clean `manager_workstation`
   install and record 100%/125% scaling plus keyboard-focus evidence.
2. Run the physical gaming-PC Agent/Shell smoke, then add permission-aware entity
   search to the command palette if it does not expose a higher-priority gap.
3. Wire real per-environment SMTP settings and work through the remaining
   pre-production decisions in the production-readiness roadmap.
