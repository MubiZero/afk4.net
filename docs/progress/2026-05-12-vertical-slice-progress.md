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
- local verification for the branch:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~CoolifyContainerDeploymentTests" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: targeted invariant tests passed, and solution build passed with
  0 warnings and 0 errors.

## Known Gaps

- Real Coolify staging now exists and passes backend health/auth smoke on the
  real `afk4.staging.mubi.dev` domain with TLS validation.
- Coolify API-created application variables were observed as build-time and
  runtime variables in this Coolify version. Before pilot or production, mark
  `ConnectionStrings__PlatformDatabase` and `Sessions__SigningPrivateKeyPem`
  runtime-only in the Coolify UI or another verified management path, then
  rotate affected staging secrets.
- Rotate the Coolify API token used for the rehearsal because it was shared in
  chat for this session.
- GitHub Actions workflows are defined and verified, but GitHub rulesets are
  not enforced for the current private repository plan. Until branch protection
  becomes available, PR merges must manually require a green
  `PR Verification Result` on the current head commit, as recorded in
  `AGENTS.md`.
- Staff management workflows, custom roles, and role editing UI are not
  implemented.
- Operator App layout management UI is not implemented.
- Automatic Agent-side consumption of rotated credentials is not implemented.
- Windows lock/unlock enforcement needs real Windows 10/11 device validation
  beyond adapter-level automated tests.
- Production Authenticode certificate authority/storage is undecided.
- Object-store/CDN provider and presigned URL automation are undecided.
- Dedicated service credential policy for update package registration is
  undecided.
- PostgreSQL restore rehearsal has a runbook but still needs a real
  staging/prod-like run before launch.
- Production lease duration and heartbeat refresh threshold need tuning after
  real Agent telemetry.

## Recommended Next Work

1. Finish PR review for `codex/coolify-staging-rehearsal`, keep the current
   head behind a green remote `PR Verification Result`, and merge only after
   explicit approval.
2. Keep enforcing the manual PR merge rule from `AGENTS.md`: current head
   commit must have a green remote `PR Verification Result`.
3. Mark Coolify app secrets runtime-only, rotate affected staging secrets, and
   keep the temporary `sslip.io` endpoint only as rehearsal evidence.
4. Run full PostgreSQL/API/Operator/Agent/Player Shell live smoke with a real
   enrolled Windows gaming PC.
5. Rehearse PostgreSQL backup, restore, and migration against staging data.
6. Choose production Authenticode certificate authority/storage, object-store
   or CDN provider, presigned URL automation, and update registration credential
   policy.
7. Harden Agent production behavior: rotated credential consumption,
   reboot/lock recovery, rollback tests, and lease timing telemetry.
8. Implement the minimum admin/configuration workflows needed for a pilot club:
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
  `codex/staging-coolify-container-deploy` was deleted after merge. The next
  recommended follow-up branch is `codex/coolify-staging-rehearsal` to run the
  Coolify deployment on the VPS and record real staging smoke evidence.

## Historical Reference

Long phase-by-phase notes, earlier test output, and old smoke evidence were
archived to keep new session context small:

- `docs/archive/progress/2026-05-12-vertical-slice-progress-history.md`
