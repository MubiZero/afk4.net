# AFK4 Progress Detail Archived During Doc Hygiene (2026-08-06)

Archived: 2026-08-06

This note preserves verbose Platform Control architecture description and
verification history removed from the compact current progress snapshot
(`docs/progress/2026-05-12-vertical-slice-progress.md`) during a doc-hygiene
pass. The Platform Control rebuild Tasks 1-7 description below documents an
intermediate architecture (organization workspace with Summary/Clubs/
Owners/Subscription/Invoices/Support/History tabs) that has since been
superseded by the fleet-pulse home screen and client-passport master-detail
redesign; it is kept here only as historical evidence, not as a description
of the current panel. It is not required startup context for new sessions.

## Platform Control rebuild Tasks 1-7 (superseded architecture)

- Platform Control rebuild Tasks 1–4 are complete on the rebuild branch: canonical
  `/admin` routing and permission-derived navigation, the operations design system,
  attention-first organization overview/list, and the full organization workspace.
  The workspace has permission-filtered Summary, Clubs, Owners/access, Subscription,
  Invoices, Support, and real read-only audit History tabs; the obsolete organization
  drawer is removed. Fresh gates: organization/App frontend 42/42, i18n 27/27,
  Platform Organization API 53/53, and the production frontend build.
- Platform Control rebuild Task 5 is complete: Plans/Subscriptions/Invoices and
  Packages/Rollouts use bookmarkable URL tabs; mutation controls follow backend
  manage permissions; Platform Support is limited to organization support and
  read-only global audit rather than billing/update operations. Global audit now
  has a permission-protected, server-filtered endpoint and browser workspace.
  Settings remains intentionally absent because no persisted platform-wide
  settings contract exists.
- Platform Control rebuild Task 6 is complete: the top bar now searches
  organizations, clubs, and organization owners through one bounded,
  permission-protected backend query. Results expose canonical links, support
  exact-ID lookup and keyboard navigation, and stale requests are cancelled and
  ignored. Existing overview attention rows continue to link to the same
  canonical organization sections. Fresh gates: Platform Control 145/145,
  platform API 157/157, shared contracts 137/137, i18n 27/27, production
  frontend build, and full solution build with 0 warnings/errors.
- Platform Control rebuild Task 7 is complete on the rebuild branch. The final
  route inventory rejects every removed compatibility URL, unreachable legacy
  components/helpers are deleted, and `/account-activation` remains the single
  current owner-invitation journey. Route-level lazy loading plus locale-specific
  catalog chunks reduced the initial production JS chunk to 309.54 kB without
  Vite's large-chunk warning. Fresh gates: Platform Control 143/143, i18n 39/39,
  shared contracts 137/137, Platform API 1476 passed with 14 PostgreSQL-only
  cases skipped, and the full solution build completed with 0 warnings/errors.
  Production-bundle Chromium assertions passed for owner organization navigation,
  global update rollouts, support permission boundaries, desktop/light and
  narrow/dark layouts, keyboard-opened mobile navigation, 200% zoom, long
  Unicode names, empty data, partial API failure, forbidden routes, and expired
  sessions. The browser run used deterministic mocked API responses and therefore
  does not claim staging deployment or live-backend integration.

## Verification Detail (pre-2026-07-30)

- Platform-managed client updates and Organization Admin reports reached
  staging at merge SHA `72dd2852` on 2026-07-29. Before the schema change,
  Coolify backup execution `unztlakrm5hbh6huonn6o6vt` completed successfully
  for `afk4_staging` (custom-format archive, 2,241,508 bytes). The database then
  advanced through `20260729062507_MoveUpdatesToPlatformControl` and
  `20260729063952_AddOrganizationAdminMaintenanceWindow`; both migration-history
  rows and both maintenance-window columns were verified, and temporary public
  database access was closed. Package Smoke run `30477386609` passed package
  build, staging MinIO publication, rollout registration, stable Organization
  Admin installer verification, and artifact upload. Migration-confirmed
  Coolify staging deploy run `30478515013` passed on the same SHA, the Platform
  API reports `running:healthy`, and a fresh public `/api/health` probe returned
  `status = ok`. Production was not touched.

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

- Stable Organization Admin compatibility download automation completed on
  2026-07-29. Package Smoke run `30420181692` published immutable internal MSI
  `0.1.122` and atomically replaced
  `organization-admin/internal/latest/afk4-organization-admin-internal.msi` in
  staging MinIO. The immutable request and public stable object both report
  SHA-256 `378eff0ea65d713de2517a512bd00e38d605a956a1e3e1b46fff2663807520b6`
  and 1,613,824 bytes; the stable response is HTTP 200 with
  `Cache-Control: no-store`. The API compatibility response is HTTP 426 and
  returns the stable URI. Publisher tests passed 13/13, the full Agent/release
  automation suite passed 185/185 on Windows, and Package Smoke triggered no
  Coolify deploy or restart. The compatibility URL was changed once through
  Coolify and restart `rvsw1xe8e7zj5rd3d63fogkj` finished healthy; subsequent
  package publications update MinIO only.

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
  findings are in `design-qa.md` (now `docs/archive/design-qa.md`).
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
  design comparison is recorded in `design-qa.md` (now `docs/archive/design-qa.md`).
  Existing React test diagnostics,
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
