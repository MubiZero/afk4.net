# AFK4 Current Progress Snapshot

Status: the first MVP-oriented vertical slice is implemented through client
packaging, signed update metadata registration automation, diagnostics, reports,
audit search, and backup/restore runbooks.

Last updated: 2026-05-18

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
- Persisted zones, seats, staff-authorized device-seat assignment, floor-map
  reads, installed app reporting, and device detail projections.
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

Staging Gaming PC setup bootstrapper branch verification on 2026-05-17:

- branch `codex/staging-gaming-pc-bootstrapper` adds a staging-only one-click
  Windows setup executable path for clean Windows 11 smoke VMs:
  `src/AFK4.GamingPc.Setup` plus testable orchestration in
  `src/AFK4.GamingPc.Setup.Core`;
- the setup executable targets `https://afk4.staging.mubi.dev`, the current
  staging organization, and the current staging branch; it asks only for staff
  username/password, creates a short-lived enrollment code, enrolls the current
  VM, installs the bundled Gaming PC MSI, writes Agent machine configuration,
  starts `AFK4.Agent.Service`, and waits for backend heartbeat evidence;
- `scripts/build-client-packages.ps1` can now emit
  `artifacts/client-packages/afk4-gaming-pc-setup-<version>-<channel>.exe`
  when supplied `-StagingLeasePublicKeyPath` from outside the repository;
- the gaming-PC package publish path now publishes Agent Service and Player
  Shell as self-contained `win-x64` outputs so a clean VM does not need a
  separate .NET Desktop Runtime install before the MSI can run;
- targeted setup tests passed locally:

  ```bash
  dotnet test tests/AFK4.GamingPc.Setup.Tests/AFK4.GamingPc.Setup.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: 9 passed, 0 failed, 0 skipped.
- targeted packaging invariant test passed locally:

  ```bash
  dotnet test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~BuildClientPackagesScript_PublishesStagingGamingPcSetupExeWithEmbeddedMsi" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: 1 passed, 0 failed, 0 skipped.
- setup project build passed locally:

  ```bash
  dotnet build src/AFK4.GamingPc.Setup/AFK4.GamingPc.Setup.csproj -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

- setup single-file `win-x64` publish with embedded dummy MSI and dummy public
  key resources passed locally, producing `AFK4.GamingPc.Setup.exe`; real VM
  packaging still requires the real Gaming PC MSI on the Windows release
  workstation.

- full solution build passed in this Linux shell when Windows targeting was
  explicitly enabled:

  ```bash
  dotnet build AFK4.sln -p:EnableWindowsTargeting=true -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

- full solution tests were attempted with `EnableWindowsTargeting=true` but
  could not complete in this Linux shell because the existing WPF test
  assemblies require `Microsoft.WindowsDesktop.App` and existing release
  automation tests invoke `powershell.exe`; these remain Windows-runner
  verification items rather than product failures.
- the staging public lease verification key is now committed at
  `deploy/coolify/staging-session-signing-public.pem`; the matching private key
  is stored only in Coolify as `Sessions__SigningPrivateKeyPem`.
- after the first Windows 11 VM bootstrapper attempt failed because the setup
  exe did not contain the embedded public-key resource, the build script was
  fixed to resolve `-StagingLeasePublicKeyPath` to an absolute path before
  passing it to MSBuild; the targeted packaging invariant test was verified
  red/green, the setup exe was rebuilt with the repo-relative key path, and
  binary inspection confirmed both the public-key resource name and public-key
  PEM payload are present in the generated exe.
- a Windows 11 VM smoke on `DESKTOP-DTMPO0V` then enrolled successfully against
  staging as device `3ba8737c-f94b-4ea4-bc25-014af468784f`; the setup UI
  observed backend heartbeat evidence, and staging device diagnostics showed
  the device online with Agent/Shell version `0.1.0`.
- the same VM exposed a packaging defect after install: WiX produced a 32-bit
  MSI, so Windows installed the Agent under `C:\Program Files (x86)\AFK4\...`
  while the bootstrapper machine config pointed
  `Agent__PlayerShellExecutablePath` at `C:\Program Files\AFK4\Player Shell`.
  `scripts/build-client-packages.ps1` now passes `-arch x64` for the
  gaming-PC MSI; the regression test was verified red/green, the staging setup
  exe was rebuilt, MSI summary metadata reports `x64;0`, and administrative
  extraction confirms `PFiles64\AFK4\Player Shell\AFK4.Player.Shell.exe` is
  present.
- the rebuilt x64 bootstrapper was rerun on the same Windows 11 VM. The Agent
  service installed under `C:\Program Files\AFK4\Agent Service`, the Player
  Shell executable existed under `C:\Program Files\AFK4\Player Shell`, and the
  setup UI enrolled staging device `bf44adb1-0681-49f5-81cc-7ceec3d371a7`
  with backend heartbeat evidence.
- a real staging session smoke then passed on that VM:
  session `90531bf3-3f37-4112-9c6a-7682e498fb9f` started as `active`,
  produced an `unlock` command accepted by the Agent, wrote
  `session-lease.json` plus `runtime-state.json` with `state=active`, and the
  visible Player Shell showed `Session is active` with remaining time.
  Ending the session produced an accepted `lock` command, cleared
  `session-lease.json`, wrote `runtime-state.json` with `state=locked`, and the
  visible Shell returned to locked.
- one manual Player Shell visibility gap was observed: the service had started
  an additional `AFK4.Player.Shell.exe` in session `0` as `NT AUTHORITY\SYSTEM`,
  while the manually visible Shell ran in the interactive user session. The
  session-0 Shell could receive named-pipe state first, leaving the visible
  Shell stale. Killing both Shell processes and restarting the visible Shell
  allowed it to receive the active/locked state. Treat this as a Player Shell
  service-supervision hardening item.
- after the lock smoke, the same staging session was manually moved from
  `ending` back to `active` in Coolify PostgreSQL only to leave the VM in an
  active visible Shell state for continued inspection. This SQL reactivation is
  not a valid production or pilot operator path. At the time, it highlighted
  that the session lifecycle needed a normal `ending`
  completion/reconciliation path so a seat/device could be reused without
  manual database edits.

Player Shell and session end hardening branch verification on 2026-05-17:

- branch `codex/staging-gaming-pc-bootstrapper` now disables Agent Service
  Player Shell auto-start by default and gates any future auto-start behind an
  explicit `Agent__PlayerShellAutoStartEnabled=true` setting plus an
  interactive user-session check. The Agent still publishes Shell state over
  the named pipe, so the current smoke/pilot path is to launch the visible
  Player Shell from the logged-in Windows desktop session.
- backend command-result processing now finalizes an `ending` session to
  `ended` when the Agent reports the matching `lock` command as accepted or
  completed. The finalization clears `CurrentLeaseId`, writes `EndedAtUtc`, and
  records a `session-ended` event. Duplicate accepted lock results do not
  create duplicate finalization events, and a second session can start on the
  same seat/device after finalization.
- a 2026-05-17 staging VM re-smoke exposed a recovery case where accepted lock
  results were already persisted while the session remained `ending`. The
  heartbeat command planner now treats an accepted/completed matching lock as a
  finalization signal before planning another lock, so stale `ending` sessions
  can converge to `ended` on the next heartbeat without SQL cleanup.
- local verification passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Agent.Service.Tests\AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~PlayerShellProcessSupervisorTests|FullyQualifiedName~RealDeviceSmokeRunbookTests" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~DeviceCommandEndpointTests --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~EfHeartbeatSessionCommandPlannerTests|FullyQualifiedName~DeviceCommandEndpointTests" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-build --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\Git\cmd\git.exe' diff --check
  ```

  Result: Agent targeted tests passed 8/8; device command endpoint tests passed
  16/16 before the heartbeat fallback and the combined heartbeat planner/device
  command endpoint regression set passed 23/23 after it; full solution build
  completed with 0 warnings and 0 errors; full no-build solution tests passed
  655/655; `git diff --check` was clean.
- at this point the branch had partial Windows 11 staging VM evidence: the
  rebuilt setup enrolled, no service-session Player Shell was running before
  the visible manual Shell launch, session start/unlock worked, and
  physical/visible lock was observed. Session reuse still needed a repeat after
  the heartbeat finalization fallback was deployed; that VM reuse gap is
  addressed by the 2026-05-18 post-redeploy smoke below.

Interactive Player Shell auto-start hardening on 2026-05-18:

- branch `codex/staging-gaming-pc-bootstrapper` now re-enables Player Shell
  auto-start by default, but no longer starts Shell from service session `0`.
  The Agent resolves the active interactive Windows console session, checks for
  an existing Shell process in that same session, and launches Shell there
  through the Windows user-token path. If no interactive session exists, Shell
  launch is skipped while heartbeat and named-pipe state publishing continue.
- process detection is now session-aware, so a stale or accidental
  `AFK4.Player.Shell.exe` in session `0` does not satisfy supervision for the
  logged-in desktop session.
- the staging Gaming PC setup writer explicitly sets
  `Agent__PlayerShellAutoStartEnabled=True` for rebuilt smoke packages.
- the real-device smoke runbook now expects Agent-driven interactive-session
  Shell auto-start. Manual Shell launch is a diagnostic fallback only after an
  auto-start or duplicate-process failure is recorded.
- local verification passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Agent.Service.Tests\AFK4.Agent.Service.Tests.csproj --filter FullyQualifiedName~PlayerShellProcessSupervisorTests --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Agent.Service.Tests\AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~PlayerShellProcessSupervisorTests|FullyQualifiedName~RealDeviceSmokeRunbookTests" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.GamingPc.Setup.Tests\AFK4.GamingPc.Setup.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-build --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\Git\cmd\git.exe' diff --check
  ```

  Result: Player Shell process supervisor tests passed 9/9; targeted
  Agent Shell/runbook tests passed 11/11; Gaming PC setup tests passed 9/9;
  full solution build completed with 0 warnings and 0 errors; full no-build
  solution tests passed 658/658; `git diff --check` was clean.

Staging VM Shell state delivery finding on 2026-05-18:

- a rebuilt staging Gaming PC setup executable installed and enrolled Windows
  11 VM device `8999549a-e1c1-4b25-8df5-ae8111d8fb97`; backend heartbeat
  reported the device online, and Agent auto-started
  `AFK4.Player.Shell.exe` in interactive user session `1` for `vm\mubi`, not
  service session `0`.
- after a staging SQL cleanup of stale session
  `329f4d61-f9c1-49f1-a3c5-6b91651ad35f`, API session start passed for
  session `992624cf-77d1-413b-8e51-6f88872183eb`; unlock command
  `4b8ac0c7-b872-4274-9379-3f7d61b79ee9` was accepted, floor map moved to
  `Active`, device lock state moved to `false`, and the VM wrote active
  `runtime-state.json` plus matching `session-lease.json`.
- the visible Player Shell remained on its initial locked screen until the
  Shell process was restarted. This confirmed a named-pipe timing race in the
  Agent-to-Shell state path: the old Agent state publisher opened a pipe only
  during a short publish window, so a running Shell could miss the active state.
- the Agent state publisher now keeps a long-lived named-pipe server with the
  latest Player Shell state. Late or restarted Shell clients receive the latest
  state on connection instead of relying on a short timing window.
- after rebuilding the staging Gaming PC setup with the long-lived state pipe,
  a second VM enrollment produced device
  `c5bda42b-77ea-4794-8523-72029c234541`. The backend reported heartbeat
  online, a manual staging SQL assignment attached it to the smoke seat, and
  session `a02f6e5e-a1aa-4dbf-8075-eaecf24efccf` started successfully. The
  visible Shell auto-started in session `1` and changed to `Session is active`
  without manual restart; VM `runtime-state.json` and `session-lease.json`
  matched the backend session id.
- ending that session initially produced lock command
  `e950241c-23c8-481f-b6a7-e302a13646cc`; the Agent accepted the command,
  local device state returned locked, and the Shell returned locked. Before
  staging was redeployed from the branch, the deployed backend still left the
  session in `Ending`.
- after Coolify staging was redeployed to commit
  `560c8a17448a52e33e366d7a0abd2990005019d5`, health returned HTTP 200 and the
  stale `Ending` session converged to locked/ended without SQL. A no-SQL reuse
  smoke then passed on the same VM/seat: session
  `06ccc56c-c615-48f0-822c-ff9e3313c2a9` started active, ended, and the floor
  map returned to `Locked` on the first heartbeat poll; session
  `6ec17520-45f3-4f63-a30d-8685d4ee5fc8` then started on the same seat/device
  without SQL cleanup. Cleanup ending of session
  `6ec17520-45f3-4f63-a30d-8685d4ee5fc8` also returned the seat to `Locked` on
  the first heartbeat poll.
- after PR #18 was merged and Coolify staging was redeployed from `main` commit
  `97dcd870d62b57eb4e6865612ada23ea9d735a22`, public health returned HTTP
  200 at `2026-05-18T09:20:08Z`. A second no-SQL main-deploy reuse smoke passed
  with run id `20260518092114`: session
  `47902334-8536-4883-98f2-f65d6e55cf6e` started active, unlock command
  `cb5f8f0c-7085-44e1-98c8-212c0c580959` was accepted, end produced lock
  command `b889e576-66ca-4137-b91d-bb5d5cf6142e`, and the seat/device reached
  `Locked` on the second poll. Session
  `d8318f5f-0607-4522-a9b5-5f0ab7324478` then started on the same seat/device
  without SQL cleanup, unlock command `7bb0b05b-4e75-46b1-a33a-038234f3b3a5`
  was accepted, cleanup lock command
  `7c582e80-1c9b-4313-959e-1fec906e9939` was accepted, and the final snapshot
  was `Locked`, no active session, device online, device locked.
- local targeted regression verification passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Agent.Service.Tests\AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~NamedPipePlayerShellStateServerTests|FullyQualifiedName~PlayerShellProcessSupervisorTests" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-build --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: targeted Agent Shell state/supervisor tests passed 11/11; full
  solution build completed with 0 warnings and 0 errors; full no-build solution
  tests passed 660/660.

Pilot device-seat assignment branch verification on 2026-05-18:

- branch `codex/pilot-admin-setup` adds a staff-authorized device-seat
  assignment API at
  `POST /api/branches/{branchId}/devices/{deviceId}/seat-assignment`.
- the endpoint requires the new `devices.seat_assignment.assign` permission,
  granted to owner, branch manager, and technician roles. It writes audit
  records for successful and denied attempts.
- assignment is desired-state/idempotent for the same active device/seat pair:
  repeated requests return the current assignment instead of creating duplicate
  active rows. Conflicting active assignments for the target seat or device are
  detached before the new assignment is written.
- assignment is rejected with `409 Conflict` while the target seat or device
  has an active, paused, or ending session, preserving session/device
  consistency.
- the staging Gaming PC setup executable now assigns the enrolled device to the
  fixed staging smoke seat through this API before installing/configuring the
  Agent. The real-device smoke runbook now uses the API assignment path and
  treats direct SQL as only a one-time fresh staging seed requirement.
- local verification passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~DeviceSeatAssignmentEndpointTests" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.GamingPc.Setup.Tests\AFK4.GamingPc.Setup.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Agent.Service.Tests\AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~RealDeviceSmokeRunbookTests" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-build --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: device-seat assignment endpoint tests passed 6/6; Gaming PC setup
  tests passed 10/10; real-device smoke runbook tests passed 2/2; full solution
  build completed with 0 warnings and 0 errors; full no-build solution tests
  passed 667/667.
- after PR #19 was merged and Coolify staging was redeployed from `main` commit
  `d0335fb760a4a6897220f185428939e3e83b0e22`, public health returned HTTP 200
  at `2026-05-18T09:55:17Z`. The deployed assignment endpoint accepted
  assigning device `c5bda42b-77ea-4794-8523-72029c234541` to smoke seat
  `9f3adbd3-957e-4dc8-8d34-a6bfa56b9275` without SQL and returned assignment
  `c16ab13c-9801-4ac3-9323-31fadd17eff4`; device detail then showed seat
  `REAL-PC-SMOKE-001`, online, and locked.
- the staging setup executable was rebuilt from current `main` and installed
  on the Windows 11 VM, enrolling device
  `0588fb59-3edb-4704-bbdb-094e12417cf1`. The setup flow assigned that device
  to smoke seat `9f3adbd3-957e-4dc8-8d34-a6bfa56b9275` through the Platform API
  and observed backend heartbeat evidence; no direct database edit was used.
  A no-SQL setup smoke then passed: session
  `eb3b44f0-6c2e-4479-b45d-5b0d2c48f7d5` started on the new device, unlock
  command `9bf9d5de-fa4b-446d-b7bb-40ea6aecebe7` was accepted, end lock command
  `7570e4f9-c35e-4557-aece-235baa420254` was accepted, and the seat returned
  to `Locked`. Session `68c787cf-77a3-4615-af2a-d77757bafe30` then started on
  the same seat/device without SQL cleanup, unlock command
  `6893a06e-3150-4e7f-a637-cf52fb6a013c` was accepted, cleanup lock command
  `564a3c9c-aebe-43a5-943a-9ad129101b7e` was accepted, and the final snapshot
  was `Locked`, no active session, new device online, and new device locked.

Centralized staging update rollout smoke on 2026-05-18:

- branch `codex/staging-agent-update-rollout-smoke` adds a Coolify-hosted
  MinIO artifact store for staging update packages. The staging update API is
  reachable at `https://updates.afk4.staging.mubi.dev`, the console is
  reachable at `https://updates-console.afk4.staging.mubi.dev`, and the
  `afk4-updates-staging` bucket is publicly readable for Agent downloads.
- `src/AFK4.Update.Publisher` and the publishing scripts now support
  S3-compatible uploads in addition to local filesystem and presigned
  `http-put` publishing. The GitHub `Package Smoke` workflow can build MSI
  packages, publish them to staging MinIO, sign update metadata, register the
  package with the staging Platform API, and create an internal device rollout.
- the staging Gaming PC setup executable now embeds the committed staging
  update package verification public key and writes
  `Agent__UpdatePackageSigningPublicKeyPem`. The setup defaults now write full
  PowerShell executable paths for install, rollback, and restart helpers so the
  Agent's configured executable existence checks pass on Windows.
- WiX now writes `Agent__AgentVersion` and `Agent__ShellVersion` from the MSI
  package version during install. The Agent recovery service recognizes that an
  interrupted self-update has already succeeded after service restart and
  reports `installed` instead of rolling back. The backend and Agent also
  normalize MSI `ProductVersion` values against prerelease metadata suffixes so
  a package such as `0.1.2-ci-smoke` is not re-offered forever after Windows
  Installer exposes `0.1.2`.
- the first staging rollout (`0.1.2-ci-minio-rollout`) proved download,
  signature verification, installer execution, and service restart, but exposed
  two staging configuration issues: an older enrolled VM lacked the update
  public key, and the installer executable was configured as `powershell.exe`
  rather than the absolute path. It also exposed the MSI/prerelease version
  mismatch above.
- after those fixes, package `449cdcf3-20fd-44cf-a94b-cfe73efcdda1` and
  rollout `8a103701-dfa1-4aec-8c63-71f479201f92` installed exact version
  `0.1.3` on staging VM device `0588fb59-3edb-4704-bbdb-094e12417cf1` through
  the Agent update pipeline. Backend rollout status reached `installed` with
  message `Interrupted update completed before Agent restart.`, device detail
  reported Agent/Shell `0.1.3`, and the VM had non-zero MSI artifacts and
  update logs under `C:\ProgramData\AFK4\Agent`.

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
- Device-seat assignment now has an authorized Platform API path and staging
  setup integration, but Operator App UI for device/seat management is not
  implemented.
- Automatic Agent-side consumption of rotated credentials is not implemented.
- Real Windows PC smoke has a repeatable staging runbook, but the runbook still
  needs to be executed on physical Windows 10/11 hardware.
- The staging one-click Gaming PC setup executable path exists in code, and the
  staging public lease verification key is committed for reproducible release
  workstation packaging. One Windows 11 VM passed rebuilt x64
  install/enroll/heartbeat plus session start/end and visible Player Shell
  state evidence. Repeat on a second clean VM or physical Windows PC before
  treating the gate as broadly validated.
- The staging setup executable is a clean-machine bootstrap path, not the
  update path for already enrolled PCs. Staging MinIO/internal MSI update
  rollout has passed on one Windows 11 VM for Agent/Shell `0.1.3`; repeat it
  on physical Windows hardware and add rollback coverage before closing the
  production update gate.
- Windows lock/unlock enforcement needs real Windows 10/11 device validation
  beyond adapter-level automated tests; if physical desktop lock/unlock does
  not occur, record that as an enforcement hardening gap rather than as a pass.
- Player Shell service-session competition and missed state delivery are
  mitigated in code and have Windows 11 VM evidence for interactive-session
  auto-start plus active-state delivery without manual Shell restart.
- Session end finalization is implemented in code for accepted/completed lock
  command results and as a heartbeat recovery fallback when an accepted lock is
  already persisted for an `ending` session. Targeted tests cover session reuse,
  duplicate results, and heartbeat convergence; post-redeploy Windows 11 VM
  smoke confirmed no-SQL reuse on the same seat/device.
- Operator App staging observation needs either a staging-configured build or a
  future runtime configuration path because the current app default API URL is
  `http://localhost:5074`.
- Production Authenticode certificate authority/storage is undecided.
- Staging update artifacts are hosted from Coolify MinIO. Production
  object-store/CDN provider, public-read policy, retention, and presigned URL
  automation are still undecided.
- Dedicated service credential policy for update package registration is
  undecided.
- PostgreSQL restore rehearsal has a runbook but still needs a real
  staging/prod-like run before launch.
- Production lease duration and heartbeat refresh threshold need tuning after
  real Agent telemetry.

## Recommended Next Work

1. Keep enforcing the manual PR merge rule from `AGENTS.md`: current head
   commit must have a green remote `PR Verification Result`.
2. Repeat the rebuilt x64 Gaming PC setup and full session start/end smoke on a
   physical Windows PC, or a second clean Windows 11 VM if physical hardware is
   unavailable, to broaden confidence beyond the current VM.
3. Execute `docs/operations/real-device-windows-pc-smoke.md` with a real
   enrolled Windows gaming PC and record actual pass/fail evidence, including
   any physical lock/unlock or Player Shell visibility gaps.
4. Rehearse PostgreSQL backup, restore, and migration against staging data.
5. Choose production Authenticode certificate authority/storage, production
   object-store or CDN provider, presigned URL automation, and update
   registration credential policy. Rotate any staging credentials that were
   exposed during manual smoke setup.
6. Repeat the internal update rollout on physical Windows hardware and add a
   rollback smoke before treating client updates as pilot-ready.
7. Harden Agent production behavior: rotated credential consumption,
   reboot/lock recovery, rollback tests, and lease timing telemetry.
8. Implement the minimum admin/configuration workflows needed for a pilot club:
   staff management, role assignment, layout management, device management UI,
   tariffs, and POS setup. Device-seat assignment now has an API path, but the
   operator-facing setup surface is still missing.

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
- PR #18, `Harden Player Shell session supervision`, merged into `main` on
  2026-05-18 with squash merge commit
  `2d7aaf093a889c7831547ef657ea0a2b962b62af`. The PR head was
  `b7f99ff02a4ecf9cc58812680a46b60f3abb86a6`, and remote
  `PR Verification Result` passed for that head in workflow run
  `26023619024`. The remote branch
  `codex/staging-gaming-pc-bootstrapper` was deleted after merge. This added
  the staging Gaming PC setup path, interactive-session Player Shell
  supervision, long-lived Agent-to-Shell state delivery, session
  end/finalization reuse hardening, and Windows 11 VM post-redeploy smoke
  evidence. The remaining runtime gate is physical Windows hardware smoke plus
  reboot/update recovery.
- PR #19, `Add pilot device-seat assignment API`, merged into `main` on
  2026-05-18 with squash merge commit
  `c5a7f64788997d22a4c2c01b456bfc50cd97f61e`. The PR head was
  `ced20d5e838e7d7eeb81cf5624481e62a485cd81`, and remote
  `PR Verification Result` passed for that head in workflow run
  `26025698119`. The remote branch `codex/pilot-admin-setup` was deleted after
  merge. This added the staff-authorized device-seat assignment API, staging
  setup assignment through the API, and runbook updates so future staging smoke
  device assignment does not require direct PostgreSQL edits.
- PR #20, `Add staging MinIO update rollout smoke`, merged into `main` on
  2026-05-19 with squash merge commit
  `ed93e0dae009fcade9b081044641e6970abb80a6`. The PR head was
  `1bc1a6346cb4ff00e05e288cfc0d58fc24adcb42`, and remote
  `PR Verification Result` passed for that head in workflow run
  `26077229854`. The first PR run exposed a Windows hosted-runner timeout in a
  PowerShell release automation test; the branch increased that test timeout
  and the rerun passed.
- after PR #20 was merged, Coolify staging was redeployed from `main` commit
  `ed93e0dae009fcade9b081044641e6970abb80a6` through deployment
  `yvl8trys8d0o6tgffd8wgawj`, which finished successfully. Public health
  returned HTTP 200 with `status = ok` at `2026-05-19T05:11:34Z`.
- the post-merge `Package Smoke` workflow on `main` passed in workflow run
  `26077441822`. It built internal MSI version `0.1.6`, published the Gaming
  PC MSI to staging MinIO for both `agent-service` and `player-shell`,
  registered update package requests with staging, and created Agent Service
  device rollout `830b5a2e-2eed-47c2-b3c8-411f05b09edf` for staging device
  `0588fb59-3edb-4704-bbdb-094e12417cf1`.
- the Windows 11 VM later installed Agent/Shell `0.1.6` from that rollout.
  The VM also exposed a stale recovery-state bug: after the successful `0.1.6`
  install, Agent recovery attempted the older `0.1.3` MSI from an old
  recoverable state file and Windows Installer rejected the downgrade with
  exit code `1603`. Branch `codex/update-recovery-superseded-state` fixes this
  by marking recoverable update states as `superseded` when the installed
  component version is already newer than the stale target, avoiding rollback
  or installer execution for older MSI artifacts.
- the same branch hardens update artifact download for sleep, reboot, network
  loss, and partial file cases. The Agent now downloads to a temporary file,
  deletes temporary partials on failure, removes stale wrong-sized final
  artifacts before retry, moves the completed artifact into place atomically,
  and can reuse an already complete staged artifact after restart.
- PR #21, `Handle superseded update recovery states`, merged into `main` on
  2026-05-19 with squash merge commit
  `55fa0911eea8c06c7b5f4315e2452edcdda5f3f1`. The PR head was
  `8a8ebfa844be4e0805cbd66c151dff4d28be8a60`, and remote
  `PR Verification Result` passed for that head in workflow run
  `26079092131`. The remote branch
  `codex/update-recovery-superseded-state` was deleted after merge.
- after PR #21 was merged, Coolify staging was redeployed from `main` commit
  `55fa0911eea8c06c7b5f4315e2452edcdda5f3f1` through deployment
  `cb1ncgnenndmkyaytowf3n3f`, which finished successfully. Public health
  returned HTTP 200 with `status = ok` at `2026-05-19T06:03:13Z`.
- the post-merge `Package Smoke` workflow on `main` passed in workflow run
  `26079268014`. It built internal MSI version `0.1.7`, published the Gaming
  PC MSI to staging MinIO for both `agent-service` and `player-shell`, and
  created Agent Service device rollout
  `84c8be00-2cfd-4e8a-8483-e78618481cbc` for staging device
  `0588fb59-3edb-4704-bbdb-094e12417cf1`. A direct HEAD request to the
  published Agent Service MSI returned HTTP 200 with a non-zero
  `Content-Length` of `58278532`; Windows VM install evidence for `0.1.7`
  is still pending.

## Historical Reference

Long phase-by-phase notes, earlier test output, and old smoke evidence were
archived to keep new session context small:

- `docs/archive/progress/2026-05-12-vertical-slice-progress-history.md`
