# AFK4 Current Progress Snapshot

Status: the first MVP-oriented vertical slice is implemented through client
packaging, signed update metadata registration automation, diagnostics, reports,
audit search, and backup/restore runbooks.

Last updated: 2026-05-17

## Purpose

This file is the compact current-state snapshot for new sessions. It replaces
the old long-form progress log, which is archived at:

- `docs/archive/progress/2026-05-12-vertical-slice-progress-history.md`

Use the archive only when historical verification details or phase-by-phase
implementation evidence are needed.

## Implemented Capabilities

### Backend Platform

- ASP.NET Core modular monolith foundation on .NET 10.
- EF Core/Npgsql persistence and migrations for identity, tenancy, devices,
  layout, sessions, billing, POS, shifts, updates, audit, diagnostics, and
  reports.
- Staff sign-in and refresh-token rotation.
- Predefined MVP role-to-permission mapping.
- Branch-scoped authorization for implemented operator-facing endpoints.
- Device enrollment, credential issuance, heartbeat validation, command
  dispatch/status, command result fallback, credential rotation, and revocation.
- Persisted zones, seats, device-seat assignments, floor-map reads, installed
  app reporting, and device detail projections.
- Session start, extend, transfer, end, signed leases, reconciliation, and
  heartbeat-driven lock/unlock/lease-refresh command planning.
- Immutable ledger-backed wallet, debt, packages, refunds, manual corrections,
  tariffs, package definitions, and package consumption.
- POS catalog, stock movements, sales, manual payments, refunds, voids,
  receipts, shifts, cash movements, and shift close reconciliation.
- Update package registration, package/rollout state changes, rollout status
  reads, device update check/status endpoints, and Agent update status tracking.
- Audit search endpoint.
- Branch diagnostics endpoint.
- Operational reports and CSV exports for shifts, sales, gameplay time, cash
  operations, and operator actions.

### Operator App

- WPF + MVVM production-oriented shell.
- Protected token storage abstraction.
- Permission-filtered navigation.
- Realtime floor map loading and selected-seat session actions.
- Player search, wallet/package summaries, POS, shifts, reports, CSV exports,
  settings, technician device tools, update package/rollout management, audit
  search, diagnostics, and production hotkeys.

### Agent Service

- Heartbeat payloads and worker loop.
- Device credential authentication.
- Realtime device hub client.
- Signed session lease validation and persistent lease/runtime state.
- Reconnect reconciliation snapshots.
- Lock/unlock enforcement coordinator through testable adapter boundaries.
- Agent host is wired for Windows Service lifetime so the WiX-registered
  `AFK4.Agent.Service` service can run under the Windows Service Control
  Manager during real-device smoke.
- Player Shell process supervision.
- Named-pipe Shell state publishing and Shell launcher command handling.
- Allow/deny process policy foundation.
- Update check/status client and background update execution worker with
  artifact download, SHA-256 verification, ECDSA metadata signature validation,
  persisted recovery state, install/rollback/restart adapter boundaries, and
  status progression.

### Player Shell

- Fullscreen WPF MVVM UI for locked, active-session, warning, grace/offline,
  ending, and launcher states.
- Receives Agent-published state through local named pipes.
- Sends launcher requests back to the Agent for validation.

### Packaging And Updates

- WiX/MSI baseline:
  - Operator App MSI.
  - Coordinated gaming-PC MSI for Agent Service + Player Shell.
- Local package build script:
  - `scripts/build-client-packages.ps1`
- Provider-neutral Authenticode signing script:
  - `scripts/sign-client-packages.ps1`
- MSI update metadata publishing script:
  - `scripts/publish-client-msi-updates.ps1`
- Backend registration script for generated update package request JSON:
  - `scripts/register-update-package-requests.ps1`
- Manual GitHub Actions workflow for build/test/package, optional signing,
  optional metadata publishing, and guarded package registration.
- Cost-aware GitHub Actions workflows:
  - PR verification with branch-protection-safe result job and conditional
    Windows build/test execution.
  - Package smoke for unsigned MSI validation on `main` and manual dispatch.
  - Manual release package workflow with short artifact retention.
  - JavaScript actions are opted into Node 24 execution with
    `FORCE_JAVASCRIPT_ACTIONS_TO_NODE24`.

### Operations Docs

- Local PostgreSQL smoke runbook.
- Coolify staging deploy runbook for building the Platform API container from
  the repo, connecting Coolify-managed PostgreSQL, applying EF migrations, and
  running health/smoke checks.
- Agent installer enrollment runbook.
- Client update rollout runbook.
- Real Windows gaming PC smoke runbook for staging Platform API, Agent Service,
  Player Shell, sessions, leases, lock/unlock evidence, installed apps,
  diagnostics, and update check/status boundaries.
- Client packaging runbook.
- Update package publishing runbook.
- PostgreSQL backup/restore rehearsal runbook.
- Production readiness roadmap.

## Latest Verification

Final verification after merging PR #9 on 2026-05-16 from `D:\afk4.net`:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Result:

- 624 passed
- 0 failed
- 0 skipped

Additional verification from the Authenticode CI registration slice:

- targeted client release automation tests passed 25/25;
- PowerShell parser checks passed for signing, publishing, and registration
  scripts;
- full solution build succeeded with 0 warnings and 0 errors;
- package smoke produced both MSI artifacts:
  - `afk4-gaming-pc-0.1.0-ci-internal.msi`
  - `afk4-operator-app-0.1.0-ci-internal.msi`
- `git diff --check` was clean before merge.

CI checks on PR #9 were not configured, so the recorded confidence is local
verification plus PR review, not enforced repository branch protection.

Cost-aware CI configuration verification on 2026-05-17:

- targeted client release automation tests passed locally;
- workflow-content tests cover PR verification, package smoke, and manual
  package workflow cost controls;
- PR #11, `Add cost-aware GitHub Actions CI`, merged into `main`;
- PR #11 remote `PR Verification` passed on GitHub before merge:
  - `Detect Relevant Changes`;
  - `Build And Test Windows`;
  - `PR Verification Result`.
- post-merge `Package Smoke` on `main` passed and uploaded unsigned MSI smoke
  artifacts.

Node 24 GitHub Actions verification on 2026-05-17:

- PR #12, `Opt GitHub Actions into Node 24`, merged into `main`;
- workflow-content tests require the Node 24 opt-in flag on all GitHub Actions
  workflows;
- PR #12 remote `PR Verification` passed on GitHub with JavaScript actions
  forced to run on Node 24;
- post-merge `Package Smoke` on `main` passed with `checkout`, `setup-dotnet`,
  and `upload-artifact` forced to run on Node 24.

Coolify staging container deploy branch verification on 2026-05-17:

- branch `codex/staging-coolify-container-deploy` adds a Platform API
  Dockerfile for Coolify repo builds, root `.dockerignore` and `.gitignore`
  secret-file guards, staging env template, fallback PostgreSQL compose
  definition, and
  `docs/operations/coolify-staging-deploy.md`;
- targeted invariant tests for the Coolify container deploy content passed
  locally:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~CoolifyContainerDeploymentTests" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: 6 passed, 0 failed, 0 skipped.
- full local solution build passed with 0 warnings and 0 errors:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

- full local no-build solution tests passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-build -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: 636 passed, 0 failed, 0 skipped.
- Platform API Release publish passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' publish src/AFK4.Platform.Api/AFK4.Platform.Api.csproj -c Release --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -o artifacts/platform-api-publish-check -v minimal
  ```

- fallback PostgreSQL compose config rendered successfully with a dummy local
  password used only for syntax validation:

  ```powershell
  $env:AFK4_STAGING_POSTGRES_PASSWORD = 'dummy-config-check-only'
  docker compose -f deploy/coolify/staging-postgres.fallback.compose.yaml config
  ```

- after Docker Desktop was started, the Platform API image built successfully
  from the Coolify repo-root context:

  ```powershell
  docker build -f src/AFK4.Platform.Api/Dockerfile -t afk4-platform-api:staging-check .
  ```

- local container smoke passed:
  - API container ran as non-root user `app`, listened on port `8080`, and
    returned `status = ok` from `/api/health` through host port `18080`;
  - fallback PostgreSQL compose resource became healthy with no public port
    published;
  - EF migrations were applied from a Linux .NET SDK one-off container on the
    same Docker network after an explicit restore inside that container;
  - PostgreSQL `__EFMigrationsHistory` contained 9 migrations through
    `20260514081906_AddUpdateRollouts`;
  - API container connected to PostgreSQL and returned the expected HTTP 401
    for a DB-backed sign-in attempt with a missing staff user;
  - staging connection strings now include `GSS Encryption Mode=Disable` to
    avoid harmless `libgssapi_krb5.so.2` fallback noise in minimal Linux
    runtime containers.
- a later repeat full runtime image rebuild reached MCR but failed on an
  external `403 Forbidden` metadata response for
  `mcr.microsoft.com/dotnet/aspnet:10.0`; the Dockerfile build stage still
  rebuilt successfully from the current tree with:

  ```powershell
  docker build --target build -f src/AFK4.Platform.Api/Dockerfile -t afk4-platform-api:staging-build-stage-check .
  ```

Coolify VPS staging rehearsal on 2026-05-17:

- branch `codex/coolify-staging-rehearsal` created the first real Coolify
  staging resources:
  - Coolify project `AFK4`, environment `staging`;
  - application `afk4-platform-api-staging`;
  - Coolify-managed PostgreSQL `afk4-staging-postgres`;
  - temporary public staging host
    `https://afk4-staging.207.180.237.97.sslip.io`.
- real staging DNS/TLS was configured after the initial rehearsal:
  `afk4.staging.mubi.dev` resolves to `207.180.237.97`, and
  `curl.exe -i https://afk4.staging.mubi.dev/api/health` returns HTTP 200
  without `curl -k`.
- preferred Coolify-managed PostgreSQL was used, not the fallback compose
  resource.
- EF migrations were applied explicitly from the release workstation after a
  temporary PostgreSQL public port was opened and then closed. Migration
  verification listed all 9 migrations through
  `20260514081906_AddUpdateRollouts`; `Test-NetConnection` confirmed the
  temporary port was closed after verification.
- Initial deploy attempts exposed two runbook/container details:
  - Coolify Dockerfile health checks execute from inside the container and need
    both `curl` and `wget` available in this Coolify version;
  - `AllowedHosts` must include `localhost` and `127.0.0.1` because Coolify's
    in-container health check calls `http://localhost:8080/api/health`.
- the branch updates the Platform API Dockerfile and Coolify runbook/template
  to cover those findings, and keeps invariant coverage in
  `CoolifyContainerDeploymentTests`.
- Coolify deploy `r9ahy05ujzwk0hjzhxpdfhlc` completed successfully for branch
  `codex/coolify-staging-rehearsal`; app and database status were both
  `running:healthy`.
- smoke evidence:

  ```powershell
  curl.exe -k -i --max-time 30 https://afk4-staging.207.180.237.97.sslip.io/api/health
  curl.exe -i --max-time 30 https://afk4.staging.mubi.dev/api/health
  ```

  Result: HTTP 200 with `{"status":"ok",...}`. The real staging domain passes
  TLS validation without the insecure curl flag.

  ```powershell
  curl.exe -k -i --max-time 30 -H "Content-Type: application/json" --data-binary "@-" https://afk4-staging.207.180.237.97.sslip.io/api/auth/staff/sign-in
  ```

  Result: HTTP 401 for a missing staff user, proving the API reached the
  migrated PostgreSQL database rather than failing with a database error.
- post-hardening verification after the Coolify API token, staging database
  password, and session signing key were rotated by the operator:

  ```powershell
  Resolve-DnsName afk4.staging.mubi.dev
  curl.exe -i --max-time 30 https://afk4.staging.mubi.dev/api/health
  curl.exe -i --max-time 30 -H "Content-Type: application/json" --data-binary "@-" https://afk4.staging.mubi.dev/api/auth/staff/sign-in
  ```

  Result: DNS still resolved to `207.180.237.97`, health returned HTTP 200
  with `status = ok`, and fake staff sign-in returned HTTP 401 against the
  rotated database/session configuration.
- local verification for the branch:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~CoolifyContainerDeploymentTests" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: targeted invariant tests passed, and solution build passed with
  0 warnings and 0 errors.

Real-device smoke preparation branch verification on 2026-05-17:

- branch `codex/real-device-smoke` adds
  `docs/operations/real-device-windows-pc-smoke.md`, links it from
  `README.md`, and adds invariant coverage in
  `RealDeviceSmokeRunbookTests`;
- the Agent Service now references
  `Microsoft.Extensions.Hosting.WindowsServices` and calls
  `AddWindowsService` with service name `AFK4.Agent.Service`, matching the
  WiX service registration used by the gaming-PC MSI;
- targeted red/green verification for the new real-device smoke invariants
  passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter FullyQualifiedName~RealDeviceSmokeRunbookTests -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: 2 passed, 0 failed, 0 skipped.
- full local solution build passed with 0 warnings and 0 errors:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

- full local no-build solution tests passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-build -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: 638 passed, 0 failed, 0 skipped.

The runbook preparation does not claim the real Windows PC smoke has been
executed. It explicitly requires operator-performed PC steps, screenshots/log
evidence, and pass/fail recording before the real-device gate can be closed.

## Known Gaps

- Real Coolify staging now exists and passes backend health/auth smoke on the
  real `afk4.staging.mubi.dev` domain with TLS validation.
- Coolify API token rotation and staging database/session secret rotation were
  completed after the rehearsal. Keep future secret handling out of chat and
  prefer Coolify UI/runtime-only settings for sensitive application variables.
- GitHub Actions workflows are defined and verified, but GitHub rulesets are
  not enforced for the current private repository plan. Until branch protection
  becomes available, PR merges must manually require a green
  `PR Verification Result` on the current head commit, as recorded in
  `AGENTS.md`.
- Staff management workflows, custom roles, and role editing UI are not
  implemented.
- Operator App layout management UI is not implemented.
- Automatic Agent-side consumption of rotated credentials is not implemented.
- Real Windows PC smoke has a repeatable staging runbook, but the runbook still
  needs to be executed on physical Windows 10/11 hardware.
- Windows lock/unlock enforcement needs real Windows 10/11 device validation
  beyond adapter-level automated tests; if physical desktop lock/unlock does
  not occur, record that as an enforcement hardening gap rather than as a pass.
- Player Shell visibility from the Agent Service still needs real user-session
  validation; the manual smoke allows a manually launched Shell for visible
  state evidence if service-started UI is not visible.
- Operator App staging observation needs either a staging-configured build or a
  future runtime configuration path because the current app default API URL is
  `http://localhost:5074`.
- Production Authenticode certificate authority/storage is undecided.
- Object-store/CDN provider and presigned URL automation are undecided.
- Dedicated service credential policy for update package registration is
  undecided.
- PostgreSQL restore rehearsal has a runbook but still needs a real
  staging/prod-like run before launch.
- Production lease duration and heartbeat refresh threshold need tuning after
  real Agent telemetry.

## Recommended Next Work

1. Keep enforcing the manual PR merge rule from `AGENTS.md`: current head
   commit must have a green remote `PR Verification Result`.
2. Execute `docs/operations/real-device-windows-pc-smoke.md` with a real
   enrolled Windows gaming PC and record actual pass/fail evidence, including
   any physical lock/unlock or Player Shell visibility gaps.
3. Rehearse PostgreSQL backup, restore, and migration against staging data.
4. Choose production Authenticode certificate authority/storage, object-store
   or CDN provider, presigned URL automation, and update registration credential
   policy.
5. Harden Agent production behavior: rotated credential consumption,
   reboot/lock recovery, rollback tests, and lease timing telemetry.
6. Implement the minimum admin/configuration workflows needed for a pilot club:
   staff management, role assignment, and layout management.

## Recent Integration Notes

- PR #9, `Add Authenticode CI update registration flow`, merged into `main` on
  2026-05-16 with merge commit
  `6f11e140d9c45b1592f71dc6c3e056fdb272c710`.
- The feature branch `codex/authenticode-ci-registration` was deleted after
  merge.
- PR #11, `Add cost-aware GitHub Actions CI`, merged into `main` on 2026-05-17
  with merge commit `8bf9d9e7823f5c932ed4ee8dd5d92b855423bdef`.
- PR #12, `Opt GitHub Actions into Node 24`, merged into `main` on 2026-05-17
  with merge commit `aa8e1b9c1d3504e9c64de6a4aa6872692728fe35`.
- PR #15, `Add Coolify staging container deploy path`, merged into `main` on
  2026-05-17 with squash merge commit
  `e1f2623bf24bf6b37d3308e2ec6778bb48fdda6d`. The PR head was
  `d761998eb086f7128bcce08ec414adba927b9fd7`, and remote
  `PR Verification Result` passed for that head in workflow run
  `25982865673`. The remote branch
  `codex/staging-coolify-container-deploy` was deleted after merge.
- PR #16, `Rehearse Coolify staging deploy`, merged into `main` on
  2026-05-17 with squash merge commit
  `e0614f6e68463224e5fca678de6e2a841eb924b6`. The PR head was
  `fb6f1b82cca2a951e0c722f0c71a2fd690f1270f`, and remote
  `PR Verification Result` passed for that head in workflow run
  `25986777751`. The remote branch `codex/coolify-staging-rehearsal` was
  deleted after merge. This completed the first real Coolify staging deploy,
  real staging DNS/TLS verification, post-rotation staging smoke, and
  container health-check hardening.
- PR #17, `Add real device smoke runbook`, merged into `main` on
  2026-05-17 with squash merge commit
  `560be2d84c5bc4099fb35e33a2c1e8a027d72740`. The PR head was
  `ae7d1b8f011017711112998f930e9bca3166f8e4`, and remote
  `PR Verification Result` passed for that head in workflow run
  `25988314062`. The remote branch `codex/real-device-smoke` was deleted after
  merge. This prepared the repeatable staging runbook for one real Windows
  gaming PC and added Windows Service host lifetime wiring for the Agent
  Service, but the real hardware smoke still needs execution and recorded
  evidence.

## Historical Reference

Long phase-by-phase notes, earlier test output, and old smoke evidence were
archived to keep new session context small:

- `docs/archive/progress/2026-05-12-vertical-slice-progress-history.md`
