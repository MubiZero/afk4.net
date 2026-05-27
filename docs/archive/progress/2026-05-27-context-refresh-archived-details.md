# AFK4 Progress Detail Archived During Context Refresh

Archived: 2026-05-27

This note preserves verbose verification and smoke evidence removed from the
compact current progress snapshot during the documentation context refresh. It
is historical evidence, not required startup context for new sessions.

## Verification Detail

- 2026-05-26 local `main` at `a28c3e4`:
  - `dotnet build .\AFK4.sln -p:EnableWindowsTargeting=true ...` passed with
    0 warnings and 0 errors.
  - `dotnet test .\AFK4.sln --no-restore -p:EnableWindowsTargeting=true ...`
    passed 1053/1053 tests.
  - `main` was pushed to `origin/main`.
- Latest recorded clean Windows 11 VM Agent baseline: internal Agent MSI
  `0.1.29` from `Package Smoke` run `26442315418`; VM2 applied the rollout,
  rebooted, kept `AFK4.Agent.Service` running, and did not reopen Setup Wizard.
- Slice 3.5 local packaging cleanup:
  - `actionlint` passed for client/package/deploy/PR workflows.
  - focused release automation tests passed 59/59.
  - default package build for `0.1.35-slice35` produced Operator App, Agent,
    and Player Shell MSI artifacts only; legacy gaming-PC MSI/setup executable
    were absent.
- 2026-05-26 `manager_workstation` VM smoke follow-up on branch
  `codex/manager-workstation-operator-env`:
  - Clean Windows 11 enrollment as `manager_workstation` completed, Agent
    Service ran, and staging rollout installed Operator App, but smoke exposed
    four local blockers: missing Operator App platform URL/bootstrap context, a
    framework-dependent Operator App MSI, WebView2 data directory creation under
    Program Files, and Operator App reaching connection resolution without the
    organization/branch IDs written by Setup Wizard.
  - Focused verification passed:
    `AFK4.Operator.App.Tests` 206/206, targeted Operator App bootstrap/options
    checks 12/12, `AFK4.SetupWizard.Tests` 11/11, and focused
    `AFK4.Agent.Service.Tests` packaging/update checks 3/3.
  - Local package build
    `scripts/build-client-packages.ps1 -Version 0.1.33-manager-env -Channel internal`
    produced `afk4-agent-0.1.33-manager-env-internal.msi`
    (`57191741` bytes,
    SHA256 `4DFA43E3994ADA195B0748C3FF05CD5CB9D354045EAC009252E4FB436FCEC393`)
    and `afk4-operator-app-0.1.33-manager-env-internal.msi`
    (`54165568` bytes,
    SHA256 `8013D8DDF1BC9CB8CDFB6D4247276D366C93B1C259FC755AB97C04E9338E5635`),
    plus Player Shell MSI artifacts.
- 2026-05-26 `manager_workstation` seatless-enrollment follow-up on branch
  `codex/manager-workstation-seatless-enroll`:
  - Fixed the remaining clean-VM model bug: `manager_workstation` enrollment no
    longer requires or creates a floor-map seat assignment. Gaming PCs still
    require a free seat.
  - Setup Wizard now selects role before optional seat selection; manager
    workstations enroll directly after branch selection, while gaming PCs move
    to free-seat selection or missing-seat creation.
  - Floor-map projection and React realtime state now ignore
    non-`gaming_pc` device assignments/statuses so old manager assignments do
    not appear as ready gaming PCs.
  - Focused verification passed:
    `AFK4.SetupWizard.Tests` 13/13, install contract serialization 4/4,
    install/floor-map Platform API tests 19/19, Operator App Web
    `floorMapState`/`operatorRealtime` tests 8/8, Operator App Web production
    build, and local client package build.
  - Full solution verification also passed: `dotnet build .\AFK4.sln` with
    0 warnings/0 errors and `dotnet test .\AFK4.sln --no-restore` with
    1061/1061 tests passing.
  - Local package build
    `scripts/build-client-packages.ps1 -Version 0.1.36-manager-seatless -Channel internal`
    produced `afk4-agent-0.1.36-manager-seatless-internal.msi`
    (`57195837` bytes,
    SHA256 `3270DF1D85F84DA3CACC1EAE0922D1DB6862279BD904D44856BF8BF5DC4622E3`)
    and `afk4-operator-app-0.1.36-manager-seatless-internal.msi`
    (`54145088` bytes,
    SHA256 `02ECD90E788F4F0869C7B1559F366E11848BDC9613F77F2EF6B80D2E5F467B0D`),
    plus Player Shell MSI artifacts.
- 2026-05-27 staging/backend tail follow-up:
  - `main` commit `cac13da` deployed to staging through `Coolify Staging
    Deploy` run `26461039901`; Coolify reported `finished`, and
    `https://afk4.staging.mubi.dev/api/health` returned `status = ok`.
  - Local follow-up fixed the package-smoke tail found by that run: staging
    package smoke registers Operator App, Agent Service, and Player Shell
    requests exactly once, creates a branch rollout for `operator-app`, keeps
    the Agent Service rollout device-targeted, and the registration script
    handles branch rollouts without device targets.
  - Floor-map reads apply the branch stale-heartbeat threshold when projecting
    device online/free state, so a deleted VM with an old heartbeat no longer
    remains `Free`/ready after the map refreshes.
  - Verification passed locally:
    `ClientReleaseAutomationTests` 42/42, floor-map Platform API tests 8/8,
    full `dotnet build .\AFK4.sln` with 0 warnings/0 errors, and full
    `dotnet test .\AFK4.sln --no-restore` with 1064/1064 tests passing.
  - Remote Package Smoke run `26507696459` on `120a5a1` passed and published
    internal package version `0.1.33`. It created the expected
    `operator-app` branch rollout `a9f01170-0171-4fbd-988b-e35076c30598` and
    kept the Agent Service rollout device-targeted with rollout
    `17c14c5b-35f4-4783-88e8-0ef29feec702`.

## Archived Gap Detail

- `manager_workstation` role smoke had local fixes and a
  `0.1.36-manager-seatless` MSI for the clean-VM blockers. Staging backend was
  updated for nullable install `SeatId`, and Package Smoke published an
  Operator App branch rollout for internal package version `0.1.33`. The
  remaining smoke work was to clean mistakenly created manager smoke
  seats/assignments and rerun the clean Windows manager-workstation path.
- Floor-map reads apply a stale heartbeat threshold after refresh. Proactive
  realtime offline broadcasts and broader inventory/detail stale-state cleanup
  remain hardening.
