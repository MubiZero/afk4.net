# AFK4 Phase 14 Diagnostics And Backup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the first production-readiness diagnostics surface for branch
operations and document PostgreSQL backup/restore rehearsal before production.

**Architecture:** Diagnostics are read-only branch-scoped read models over the
existing modular monolith persistence. They use a new `diagnostics.view`
permission and audit read attempts. PostgreSQL backup/restore is captured as an
operations runbook, not automated provider infrastructure.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, EF Core/Npgsql,
AFK4.Shared.Contracts DTOs, WPF MVVM Operator App, xUnit, PowerShell,
PostgreSQL CLI tooling.

---

## Scope

Phase 14 implements:

- a focused diagnostics design spec and plan;
- shared diagnostics DTO contracts;
- backend branch diagnostics service and endpoint;
- `diagnostics.view` permission and default role mapping;
- audit records for allowed and denied diagnostics reads;
- Operator App diagnostics API client, ViewModel, navigation visibility, and
  Settings panel;
- PostgreSQL backup/restore runbook;
- README and progress updates.

Phase 14 does not implement:

- background alerting;
- provider-specific metrics exporters;
- provider-specific managed PostgreSQL backup APIs;
- tenant data export;
- web admin;
- local club server;
- database schema changes.

## File Structure

Create and modify these files:

```text
D:\afk4.net\
  README.md
  docs\operations\postgres-backup-restore.md
  docs\progress\2026-05-12-vertical-slice-progress.md
  docs\superpowers\specs\2026-05-16-afk4-diagnostics-backup-design.md
  docs\superpowers\plans\2026-05-16-afk4-phase14-diagnostics-backup.md
  src\AFK4.Shared.Contracts\Diagnostics\
    BranchDiagnosticsDto.cs
  src\AFK4.Shared.Contracts\Identity\StaffPermissionNames.cs
  src\AFK4.Platform.Api\Audit\AuditActionNames.cs
  src\AFK4.Platform.Api\Diagnostics\
    BranchDiagnosticsOptions.cs
    EfBranchDiagnosticsService.cs
    IBranchDiagnosticsService.cs
  src\AFK4.Platform.Api\Identity\PermissionCatalog.cs
  src\AFK4.Platform.Api\Program.cs
  src\AFK4.Operator.App\Diagnostics\
    DiagnosticsWorkspaceViewModel.cs
    HttpOperatorDiagnosticsApiClient.cs
    IOperatorDiagnosticsApiClient.cs
    UnconfiguredOperatorDiagnosticsApiClient.cs
  src\AFK4.Operator.App\OperatorShellViewModel.cs
  src\AFK4.Operator.App\MainWindow.xaml
  tests\AFK4.Shared.Contracts.Tests\
    DiagnosticsContractSerializationTests.cs
  tests\AFK4.Platform.Api.Tests\
    BranchDiagnosticsEndpointTests.cs
    EfBranchDiagnosticsServiceTests.cs
  tests\AFK4.Operator.App.Tests\
    DiagnosticsWorkspaceViewModelTests.cs
    OperatorDiagnosticsApiClientTests.cs
```

## Task 1: Documentation Decision

**Files:**

- Create: `docs\superpowers\specs\2026-05-16-afk4-diagnostics-backup-design.md`
- Create: `docs\superpowers\plans\2026-05-16-afk4-phase14-diagnostics-backup.md`
- Modify: `docs\progress\2026-05-12-vertical-slice-progress.md`

- [ ] **Step 1: Record diagnostics and backup design**

Create the design spec with these decisions:

- diagnostics are branch-scoped and read-only;
- first endpoint is `GET /api/branches/{branchId}/diagnostics`;
- access uses `diagnostics.view`;
- diagnostics reads write audit records;
- first response uses existing devices, command, update status, and rollout
  persistence;
- backup/restore is a PostgreSQL runbook in this phase.

- [ ] **Step 2: Add this implementation plan**

Save this plan at:

```text
docs/superpowers/plans/2026-05-16-afk4-phase14-diagnostics-backup.md
```

- [ ] **Step 3: Update progress navigation**

Add the Phase 14 plan to the progress document plan list and record that Phase
14 has started from the remaining diagnostics/backup operations item.

- [ ] **Step 4: Verify documentation references**

Run:

```powershell
rg -n "Phase 14|diagnostics.view|postgres-backup-restore|diagnostics-backup" docs README.md
```

Expected: the new spec, new plan, and progress document are found.

- [ ] **Step 5: Commit documentation decision**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add docs
& 'C:\Program Files\Git\cmd\git.exe' commit -m "docs: add diagnostics backup plan"
```

Expected:

```text
[codex/phase11-operational-reports ...] docs: add diagnostics backup plan
```

## Task 2: Shared Diagnostics Contracts And Permission

**Files:**

- Create: `src\AFK4.Shared.Contracts\Diagnostics\BranchDiagnosticsDto.cs`
- Create: `tests\AFK4.Shared.Contracts.Tests\DiagnosticsContractSerializationTests.cs`
- Modify: `src\AFK4.Shared.Contracts\Identity\StaffPermissionNames.cs`

- [ ] **Step 1: Write failing contract serialization tests**

Cover branch diagnostics response round-trip and expose
`StaffPermissionNames.ViewDiagnostics == "diagnostics.view"`.

- [ ] **Step 2: Add diagnostics DTOs**

Add immutable DTO records for:

- `BranchDiagnosticsDto`;
- `DeviceDiagnosticsSummaryDto`;
- `CommandDiagnosticsSummaryDto`;
- `UpdateDiagnosticsSummaryDto`;
- `StaleDeviceDiagnosticsDto`;
- `FailedCommandDiagnosticsDto`;
- `FailedUpdateDiagnosticsDto`.

- [ ] **Step 3: Run contract tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Shared.Contracts.Tests\AFK4.Shared.Contracts.Tests.csproj --filter DiagnosticsContractSerializationTests -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
```

Expected: diagnostics contract tests pass.

## Task 3: Backend Diagnostics Service And Endpoint

**Files:**

- Create: `src\AFK4.Platform.Api\Diagnostics\BranchDiagnosticsOptions.cs`
- Create: `src\AFK4.Platform.Api\Diagnostics\EfBranchDiagnosticsService.cs`
- Create: `src\AFK4.Platform.Api\Diagnostics\IBranchDiagnosticsService.cs`
- Create: `tests\AFK4.Platform.Api.Tests\EfBranchDiagnosticsServiceTests.cs`
- Create: `tests\AFK4.Platform.Api.Tests\BranchDiagnosticsEndpointTests.cs`
- Modify: `src\AFK4.Platform.Api\Audit\AuditActionNames.cs`
- Modify: `src\AFK4.Platform.Api\Identity\PermissionCatalog.cs`
- Modify: `src\AFK4.Platform.Api\Program.cs`

- [ ] **Step 1: Write failing service tests**

Assert that the EF service:

- filters by organization and branch;
- counts total, online, locked, stale, and newest-heartbeat devices;
- counts pending and failed commands for branch devices;
- returns recent failed command rows with machine names;
- counts active rollouts and failed/installing/rollback update statuses;
- returns recent failed update rows with machine names.

- [ ] **Step 2: Implement service**

Build diagnostics from existing EF entities with no schema change. Use
`TimeProvider` and a default five-minute stale threshold.

- [ ] **Step 3: Write failing endpoint tests**

Assert:

- anonymous reads return `401`;
- cashier reads return `403` and denied audit;
- technician reads return diagnostics and succeeded audit;
- accountant/auditor reads are allowed;
- cross-branch rows are excluded.

- [ ] **Step 4: Implement endpoint and RBAC**

Register the diagnostics service, add `diagnostics.view` to the permission
catalog roles defined in the design, and map:

```text
GET /api/branches/{branchId}/diagnostics
```

- [ ] **Step 5: Run targeted backend tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter "EfBranchDiagnosticsServiceTests|BranchDiagnosticsEndpointTests" -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
```

Expected: targeted backend diagnostics tests pass.

## Task 4: Operator App Diagnostics Workspace

**Files:**

- Create: `src\AFK4.Operator.App\Diagnostics\DiagnosticsWorkspaceViewModel.cs`
- Create: `src\AFK4.Operator.App\Diagnostics\HttpOperatorDiagnosticsApiClient.cs`
- Create: `src\AFK4.Operator.App\Diagnostics\IOperatorDiagnosticsApiClient.cs`
- Create: `src\AFK4.Operator.App\Diagnostics\UnconfiguredOperatorDiagnosticsApiClient.cs`
- Create: `tests\AFK4.Operator.App.Tests\DiagnosticsWorkspaceViewModelTests.cs`
- Create: `tests\AFK4.Operator.App.Tests\OperatorDiagnosticsApiClientTests.cs`
- Modify: `src\AFK4.Operator.App\OperatorShellViewModel.cs`
- Modify: `src\AFK4.Operator.App\MainWindow.xaml`
- Modify: existing Operator App shell/settings tests as needed.

- [ ] **Step 1: Write failing API client and ViewModel tests**

Assert:

- typed client calls `/api/branches/{branchId}/diagnostics` with staff bearer
  token;
- ViewModel loads summary counts and recent failure rows;
- invalid or missing branch context returns a visible error without calling the
  API;
- shell exposes diagnostics only with `diagnostics.view`.

- [ ] **Step 2: Implement typed client and ViewModel**

Follow existing audit/update workspace patterns. Keep the ViewModel testable
without WPF rendering.

- [ ] **Step 3: Add Settings UI**

Add a compact diagnostics panel with summary counters and data grids for stale
devices, failed commands, and failed updates.

- [ ] **Step 4: Run targeted Operator App tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Operator.App.Tests\AFK4.Operator.App.Tests.csproj --filter "OperatorDiagnosticsApiClientTests|DiagnosticsWorkspaceViewModelTests|SettingsWorkspaceViewModelTests|OperatorShellViewModelTests" -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
```

Expected: targeted Operator App diagnostics tests pass.

## Task 5: PostgreSQL Backup And Restore Runbook

**Files:**

- Create: `docs\operations\postgres-backup-restore.md`
- Modify: `README.md`
- Modify: `docs\progress\2026-05-12-vertical-slice-progress.md`

- [ ] **Step 1: Add runbook**

Document:

- required tools and environment variables;
- custom-format `pg_dump`;
- restore into a new database with `pg_restore`;
- migration script generation;
- staging rehearsal;
- smoke checks after restore;
- retention and encryption expectations;
- source-control secret restrictions;
- append-only audit/ledger recovery boundaries.

- [ ] **Step 2: Link runbook**

Update README operations links and progress status.

- [ ] **Step 3: Verify docs**

Run:

```powershell
rg -n "postgres-backup-restore|pg_dump|pg_restore|Phase 14|diagnostics.view" README.md docs
```

Expected: README, runbook, and progress references are found.

## Task 6: Full Verification And Progress

**Files:**

- Modify: `README.md`
- Modify: `docs\progress\2026-05-12-vertical-slice-progress.md`

- [ ] **Step 1: Run targeted tests**

Run all targeted commands from Tasks 2 through 4.

- [ ] **Step 2: Run full build and tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected:

```text
Build succeeded.
Passed! - Failed: 0
```

- [ ] **Step 3: Update progress**

Record implemented diagnostics surface, runbook, exact verification commands,
and next recommended work.

- [ ] **Step 4: Commit implementation and verification docs**

Use coherent commits, for example:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add branch diagnostics"
& 'C:\Program Files\Git\cmd\git.exe' add README.md docs
& 'C:\Program Files\Git\cmd\git.exe' commit -m "docs: add backup restore runbook"
```

## Plan Self-Review

Spec coverage:

- Backend diagnostics endpoint is covered by Tasks 2 and 3.
- Operator diagnostics dashboard is covered by Task 4.
- Backup/restore runbook is covered by Task 5.
- Verification and progress tracking are covered by Task 6.

Deferred:

- metrics exporters;
- alerting;
- provider-specific managed PostgreSQL backup/PITR APIs;
- tenant data export;
- web admin diagnostics;
- local club server diagnostics.

