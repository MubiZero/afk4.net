# AFK4 Phase 9 Updates And Installers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the first safe centralized update foundation for AFK4 client software: signed package metadata, stable/beta/internal channels, rollout targeting, device update checks, device status reporting, Agent-side update client boundaries, and installer/enrollment runbooks.

**Architecture:** Keep the Cloud Backend as the update authority. The Agent checks for cloud-approved updates and reports progress, but it does not decide rollout eligibility. Package binaries and signing keys stay outside source control. This phase starts with metadata, targeting, status, and Agent boundaries; actual installer build pipelines and production package storage can evolve behind those contracts.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, EF Core/Npgsql, shared DTO contracts, Windows Agent Service HTTP client boundaries, xUnit, existing device credential authentication and staff permission/audit infrastructure.

---

## Scope

Phase 9 implements:

- shared update contracts for package metadata, rollout targeting, device update checks, and device status reports;
- backend update package metadata persistence with required hash/signature fields;
- backend rollout persistence with stable, beta, and internal channels;
- first rollout target support for branch-wide and explicit device targets;
- staff-protected endpoints for registering packages, creating rollouts, and reading rollout status;
- device-credential endpoints for Agent update checks and update status reporting;
- Agent-side update client/status reporter boundary;
- installer/enrollment and update safety runbooks;
- focused verification and a local smoke path.

Phase 9 does not implement:

- web admin, local club server, microservices, non-Windows agents, or kernel drivers;
- country-specific fiscal/payment integrations;
- full binary artifact hosting or CI installer build automation inside this repo;
- automatic Steam/Epic/Battle.net game updates;
- production signing key storage;
- applying in-place Agent executable replacement while the service is running beyond a testable adapter boundary.

## Current Baseline

Start from `codex-phase9-updates-installers` after Phase 8 verification commit
`24ed9b2 docs: record phase 8 verification`.

Already available and reused:

- staff authentication, branch-scoped permissions, and audit writer;
- device enrollment, device credential validation, heartbeat, installed-app reporting, and command status tracking;
- Agent HTTP client patterns for heartbeat, installed apps, and reconciliation;
- Operator technician device workflows where update status can later be surfaced;
- Player Shell and Agent runtime version reporting through heartbeat fields.

Known baseline gaps Phase 9 addresses:

- no update package, channel, rollout, or rollback state is stored;
- no Agent endpoint exists for checking rollout eligibility;
- no Agent endpoint exists for update progress/status reporting;
- no installer/enrollment runbook describes the intended operational bootstrap;
- permissions do not yet include update package or rollout management.

## File Structure

Create and modify these files:

```text
D:\afk4.net\
  README.md
  docs\progress\2026-05-12-vertical-slice-progress.md
  docs\operations\agent-installer-enrollment.md
  docs\operations\client-update-rollout.md
  docs\superpowers\plans\2026-05-14-afk4-phase9-updates-installers.md
  src\AFK4.Shared.Contracts\
    Updates\ComponentUpdateInstructionDto.cs
    Updates\CreateUpdatePackageRequest.cs
    Updates\CreateUpdateRolloutRequest.cs
    Updates\DeviceComponentVersionDto.cs
    Updates\DeviceUpdateCheckRequest.cs
    Updates\DeviceUpdateCheckResponse.cs
    Updates\DeviceUpdateStatusReportRequest.cs
    Updates\DeviceUpdateStatusResultDto.cs
    Updates\UpdateChannelNames.cs
    Updates\UpdateComponentNames.cs
    Updates\UpdatePackageDto.cs
    Updates\UpdatePackageStateNames.cs
    Updates\UpdateRolloutDto.cs
    Updates\UpdateRolloutStateNames.cs
    Updates\UpdateStatusNames.cs
    Updates\UpdateTargetKindNames.cs
  src\AFK4.Platform.Api\
    Program.cs
    Audit\AuditActionNames.cs
    Data\DeviceUpdateStatusEntity.cs
    Data\PlatformDbContext.cs
    Data\UpdatePackageEntity.cs
    Data\UpdateRolloutEntity.cs
    Data\UpdateRolloutTargetEntity.cs
    Data\Migrations\...
    Identity\PermissionCatalog.cs
    Updates\EfUpdateService.cs
    Updates\IUpdateService.cs
    Updates\UpdateServiceResult.cs
  src\AFK4.Agent.Service\
    AgentOptions.cs
    Program.cs
    Updates\HttpAgentUpdateClient.cs
    Updates\IAgentUpdateClient.cs
    Updates\UpdateCheckResult.cs
  tests\
    AFK4.Shared.Contracts.Tests\UpdateContractSerializationTests.cs
    AFK4.Platform.Api.Tests\EfUpdateServiceTests.cs
    AFK4.Platform.Api.Tests\UpdateEndpointTests.cs
    AFK4.Agent.Service.Tests\HttpAgentUpdateClientTests.cs
```

Responsibility boundaries:

- `AFK4.Shared.Contracts.Updates`: transport DTOs only. No backend entities or Agent-only models.
- `AFK4.Platform.Api.Updates`: update package/rollout/status application rules.
- `AFK4.Platform.Api.Data`: EF persistence for packages, rollouts, targets, and per-device status.
- `AFK4.Agent.Service.Updates`: Agent HTTP boundary for checking updates and reporting status. Actual installation remains behind a later adapter.
- `docs/operations`: human-readable operational runbooks for installer/enrollment and rollout safety.

## Update Model

Components:

```text
operator-app
agent-service
player-shell
```

Channels:

```text
stable
beta
internal
```

Package states:

```text
registered
validated
rejected
retired
```

Rollout states:

```text
draft
active
paused
completed
rollback-requested
rolled-back
cancelled
```

Device update statuses:

```text
not-started
offered
downloading
downloaded
installing
installed
failed
rollback-started
rolled-back
```

Safety rules:

- production/stable rollouts require package hash and signature metadata;
- packages are immutable after registration except state transitions;
- a rollout targets exactly one package and one channel;
- a branch-wide rollout never crosses organization/branch boundaries;
- device status reports must be authenticated with that device credential;
- status reports must not mutate package metadata or rollout targeting;
- rollback is represented as explicit rollout/status state, not deletion.

## Task 1: Phase 9 Plan And Branch Setup

**Files:**

- Create: `docs\superpowers\plans\2026-05-14-afk4-phase9-updates-installers.md`
- Modify: `docs\progress\2026-05-12-vertical-slice-progress.md`

- [ ] **Step 1: Create branch**

```powershell
& 'C:\Program Files\Git\cmd\git.exe' switch -c codex-phase9-updates-installers
```

- [ ] **Step 2: Add focused plan**

Write this plan before implementation so the Phase 9 scope is explicit.

- [ ] **Step 3: Commit plan**

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add docs/superpowers/plans/2026-05-14-afk4-phase9-updates-installers.md docs/progress/2026-05-12-vertical-slice-progress.md
& 'C:\Program Files\Git\cmd\git.exe' commit -m "docs: add phase 9 updates plan"
```

## Task 2: Shared Update Contracts

**Files:**

- Create: `src\AFK4.Shared.Contracts\Updates\*.cs`
- Create: `tests\AFK4.Shared.Contracts.Tests\UpdateContractSerializationTests.cs`

- [ ] **Step 1: Write failing serialization tests**

Cover:

- package registration request and package DTO;
- rollout creation request and rollout DTO;
- device update check request/response with an available update;
- device update status report/result.

- [ ] **Step 2: Run tests and verify RED**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter UpdateContractSerializationTests --no-restore -p:UseSharedCompilation=false
```

- [ ] **Step 3: Implement contracts**

Keep DTOs immutable records and keep string constants in explicit `*Names`
classes.

- [ ] **Step 4: Run tests and commit**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter UpdateContractSerializationTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Shared.Contracts tests/AFK4.Shared.Contracts.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add update rollout contracts"
```

## Task 3: Backend Update Persistence And Service

**Files:**

- Modify: `src\AFK4.Platform.Api\Data\PlatformDbContext.cs`
- Create: `src\AFK4.Platform.Api\Data\UpdatePackageEntity.cs`
- Create: `src\AFK4.Platform.Api\Data\UpdateRolloutEntity.cs`
- Create: `src\AFK4.Platform.Api\Data\UpdateRolloutTargetEntity.cs`
- Create: `src\AFK4.Platform.Api\Data\DeviceUpdateStatusEntity.cs`
- Create: `src\AFK4.Platform.Api\Updates\IUpdateService.cs`
- Create: `src\AFK4.Platform.Api\Updates\EfUpdateService.cs`
- Create: `src\AFK4.Platform.Api\Updates\UpdateServiceResult.cs`
- Create: `tests\AFK4.Platform.Api.Tests\EfUpdateServiceTests.cs`

- [ ] **Step 1: Write failing service tests**

Cover:

- package registration requires component, version, artifact URI, SHA-256 hash, and signature;
- duplicate package component/version/channel is rejected within a branch;
- rollout creation stores branch-wide and explicit-device targets;
- device check returns only active rollouts scoped to the device organization/branch;
- status report creates or updates one per-device package/rollout status row.

- [ ] **Step 2: Implement entities and service**

Use EF-backed rules. Keep package metadata immutable after creation.

- [ ] **Step 3: Add EF migration**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' ef migrations add AddUpdateRollouts --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
```

- [ ] **Step 4: Run service tests and commit**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfUpdateServiceTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add update rollout persistence"
```

## Task 4: Backend Update Endpoints And Permissions

**Files:**

- Modify: `src\AFK4.Shared.Contracts\Identity\StaffPermissionNames.cs`
- Modify: `src\AFK4.Platform.Api\Identity\PermissionCatalog.cs`
- Modify: `src\AFK4.Platform.Api\Audit\AuditActionNames.cs`
- Modify: `src\AFK4.Platform.Api\Program.cs`
- Create: `tests\AFK4.Platform.Api.Tests\UpdateEndpointTests.cs`

- [ ] **Step 1: Write failing endpoint tests**

Cover:

- staff package registration requires `updates.packages.manage`;
- staff rollout creation requires `updates.rollouts.manage`;
- staff rollout status read requires `updates.status.view`;
- denied staff attempts write audit records;
- device update check requires valid device credential;
- device status report requires valid device credential and route/request identity match.

- [ ] **Step 2: Implement endpoints**

Initial routes:

```text
POST /api/branches/{branchId}/updates/packages
POST /api/branches/{branchId}/updates/rollouts
GET  /api/branches/{branchId}/updates/rollouts/{rolloutId}
POST /api/devices/{deviceId}/updates/check
POST /api/devices/{deviceId}/updates/status
```

- [ ] **Step 3: Run endpoint tests and commit**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter UpdateEndpointTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Shared.Contracts src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: expose update rollout endpoints"
```

## Task 5: Agent Update Client Boundary

**Files:**

- Modify: `src\AFK4.Agent.Service\AgentOptions.cs`
- Modify: `src\AFK4.Agent.Service\Program.cs`
- Create: `src\AFK4.Agent.Service\Updates\IAgentUpdateClient.cs`
- Create: `src\AFK4.Agent.Service\Updates\HttpAgentUpdateClient.cs`
- Create: `src\AFK4.Agent.Service\Updates\UpdateCheckResult.cs`
- Create: `tests\AFK4.Agent.Service.Tests\HttpAgentUpdateClientTests.cs`

- [ ] **Step 1: Write failing Agent client tests**

Cover:

- update check posts installed component versions with device credential;
- status report posts rollout/package status with device credential;
- empty update response is handled as no available updates.

- [ ] **Step 2: Implement client boundary**

Do not install binaries yet. The Agent client only checks, reports, and exposes a typed boundary for a later installer adapter.

- [ ] **Step 3: Run Agent tests and commit**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter HttpAgentUpdateClientTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Agent.Service tests/AFK4.Agent.Service.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add agent update client boundary"
```

## Task 6: Installer And Rollout Runbooks

**Files:**

- Create: `docs\operations\agent-installer-enrollment.md`
- Create: `docs\operations\client-update-rollout.md`
- Modify: `README.md`

- [ ] **Step 1: Document gaming-PC installer enrollment**

Cover secure enrollment code input, device credential storage expectation, Agent/Shell install order, service startup, and rollback-safe manual recovery.

- [ ] **Step 2: Document update rollout workflow**

Cover package registration, signature/hash requirement, internal/beta/stable channel progression, staged branch/device targeting, status monitoring, rollback, and actions to take if Agent update fails.

- [ ] **Step 3: Commit runbooks**

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add README.md docs/operations
& 'C:\Program Files\Git\cmd\git.exe' commit -m "docs: add installer update runbooks"
```

## Task 7: Phase 9 Verification

**Files:**

- Modify: `README.md`
- Modify: `docs\progress\2026-05-12-vertical-slice-progress.md`

- [ ] **Step 1: Run targeted tests**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter UpdateContractSerializationTests -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "EfUpdateServiceTests|UpdateEndpointTests" -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter HttpAgentUpdateClientTests -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
```

- [ ] **Step 2: Run full verification**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln -p:NuGetAudit=false -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false -v minimal
```

- [ ] **Step 3: Run local smoke**

Use a temporary PostgreSQL database or the existing local smoke pattern to verify:

- package registration;
- rollout creation;
- device update check;
- device status report.

- [ ] **Step 4: Commit verification docs**

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add README.md docs/progress/2026-05-12-vertical-slice-progress.md
& 'C:\Program Files\Git\cmd\git.exe' commit -m "docs: record phase 9 verification"
```

## Plan Self-Review

Spec coverage:

- signed packages are represented by required artifact hash and signature metadata;
- stable, beta, and internal channels are explicit constants and persisted rollout fields;
- branch-wide and explicit-device rollout targeting are included;
- rollback is represented through explicit rollout/status states;
- device update checks and reports use device credential authentication;
- staff update management is permission-gated and audited;
- Agent update behavior starts as a testable HTTP boundary before installer execution is introduced.

Deferred from this plan:

- binary artifact storage provider;
- production signing key management;
- CI-generated MSI/MSIX artifacts;
- in-place Agent process replacement;
- Operator App update management UI;
- reports, audit search, diagnostics dashboards, and backup/restore runbooks.

These remain deferred so the first update slice can establish authority, state, targeting, and safety contracts before adding installer automation.
