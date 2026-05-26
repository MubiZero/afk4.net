# AFK4 Heartbeat Lease Refresh Follow-Up Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make device heartbeat recover and refresh active session leases when the backend still has an authoritative active session but the Agent reports a missing, stale, or near-expiry local lease.

**Architecture:** Keep the Sessions module as the only authority for active session and lease decisions. `DeviceHeartbeatService` remains responsible for recording device status and returning pending device commands, but it asks a session-owned planner whether heartbeat state requires `unlock`, `refresh-session-lease`, or `lock` commands. The Agent remains an executor of backend-signed leases and never creates, extends, or signs leases locally.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, EF Core/Npgsql, SignalR device commands, xUnit.

---

## Scope

This follow-up implements one hardening idea preserved from the deleted
`codex/phase8-agent-enforcement-player-shell` branch: proactive lease command
planning from device heartbeat.

It covers:

- active backend session plus missing Agent lease should enqueue `unlock` with a fresh signed lease;
- active backend session plus near-expiry Agent lease should enqueue `refresh-session-lease`;
- active backend session plus healthy current Agent lease should not enqueue duplicate commands;
- ending or ended backend session plus active Agent lease should enqueue `lock`;
- heartbeat command planning should be idempotent enough not to flood devices with duplicate pending session commands;
- command type names should stop being repeated as magic strings in backend and Agent paths touched by this work.

It does not cover:

- starting, extending, transferring, or ending sessions offline;
- changing billing, package, tariff, POS, or shift behavior;
- changing Agent lease signing or validation authority;
- changing Player Shell trust boundaries;
- local server, web admin, microservices, or non-Windows Agents.

## Current Baseline

Current `main` already has most Phase 8 runtime work:

- `DeviceHeartbeatRequest` reports `ActiveSessionId`,
  `ActiveSessionLeaseExpiresAtUtc`, and `ActiveSessionLeaseSequence`;
- `DeviceHeartbeatService` persists heartbeat state and returns existing
  pending `device_commands`;
- `EfSessionCommandService` issues `unlock`, `lock`, and
  `refresh-session-lease` commands for explicit session start, end, transfer,
  and extend flows;
- `SessionLeaseOptions.LeaseMinutes` is configurable, with current default
  value `15`;
- Agent-side command handling accepts `refresh-session-lease` and replaces the
  local persisted lease.

Current gap:

- heartbeat does not compare the Agent lease snapshot with authoritative cloud
  session state, so a long-running session can depend on explicit operator
  actions or reconnect reconciliation to receive a new lease.

## File Structure

Create and modify these files:

```text
D:\afk4.net\
  docs\progress\2026-05-12-vertical-slice-progress.md

  src\AFK4.Shared.Contracts\
    Devices\DeviceCommandTypeNames.cs

  src\AFK4.Platform.Api\
    Devices\DeviceHeartbeatService.cs
    Program.cs
    Sessions\HeartbeatSessionCommandPlan.cs
    Sessions\IHeartbeatSessionCommandPlanner.cs
    Sessions\EfHeartbeatSessionCommandPlanner.cs
    Sessions\EfSessionCommandService.cs

  src\AFK4.Agent.Service\
    DefaultDeviceCommandHandler.cs

  tests\AFK4.Shared.Contracts.Tests\
    DeviceCommandTypeNamesTests.cs

  tests\AFK4.Platform.Api.Tests\
    EfHeartbeatSessionCommandPlannerTests.cs
    DeviceHeartbeatLeaseRefreshTests.cs
```

Responsibility boundaries:

- `DeviceCommandTypeNames` defines stable command strings shared by backend,
  Agent, and tests.
- `IHeartbeatSessionCommandPlanner` belongs to Sessions and owns decisions
  derived from active sessions and session leases.
- `DeviceHeartbeatService` belongs to Devices and only orchestrates heartbeat
  persistence plus command enqueue/return.
- `EfSessionCommandService` keeps explicit operator-driven session command
  behavior; the new planner reuses the same command semantics but does not
  authorize new business actions.

## Task 1: Shared Device Command Type Names

**Files:**

- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Devices\DeviceCommandTypeNames.cs`
- Create: `D:\afk4.net\tests\AFK4.Shared.Contracts.Tests\DeviceCommandTypeNamesTests.cs`
- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Sessions\EfSessionCommandService.cs`
- Modify: `D:\afk4.net\src\AFK4.Agent.Service\DefaultDeviceCommandHandler.cs`

- [ ] **Step 1: Write the failing command-name test**

Create `D:\afk4.net\tests\AFK4.Shared.Contracts.Tests\DeviceCommandTypeNamesTests.cs`:

```csharp
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Shared.Contracts.Tests;

public sealed class DeviceCommandTypeNamesTests
{
    [Fact]
    public void SessionCommandNames_AreStableTransportStrings()
    {
        Assert.Equal("lock", DeviceCommandTypeNames.Lock);
        Assert.Equal("unlock", DeviceCommandTypeNames.Unlock);
        Assert.Equal("refresh-session-lease", DeviceCommandTypeNames.RefreshSessionLease);
    }
}
```

- [ ] **Step 2: Run the test and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Shared.Contracts.Tests\AFK4.Shared.Contracts.Tests.csproj --filter DeviceCommandTypeNamesTests --no-restore -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
```

Expected:

```text
error CS0103: The name 'DeviceCommandTypeNames' does not exist in the current context
```

- [ ] **Step 3: Add the shared command names**

Create `D:\afk4.net\src\AFK4.Shared.Contracts\Devices\DeviceCommandTypeNames.cs`:

```csharp
namespace AFK4.Shared.Contracts.Devices;

public static class DeviceCommandTypeNames
{
    public const string Lock = "lock";

    public const string Unlock = "unlock";

    public const string RefreshSessionLease = "refresh-session-lease";
}
```

- [ ] **Step 4: Replace touched magic strings**

Replace direct string literals for `lock`, `unlock`, and
`refresh-session-lease` in:

- `D:\afk4.net\src\AFK4.Platform.Api\Sessions\EfSessionCommandService.cs`
- `D:\afk4.net\src\AFK4.Agent.Service\DefaultDeviceCommandHandler.cs`

Only replace device command type comparisons or command creation values. Do not
change audit action names, UI labels, test names, or prose.

- [ ] **Step 5: Run targeted tests and commit**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Shared.Contracts.Tests\AFK4.Shared.Contracts.Tests.csproj --filter DeviceCommandTypeNamesTests --no-restore -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Agent.Service.Tests\AFK4.Agent.Service.Tests.csproj --filter "DefaultDeviceCommandHandlerTests|SessionCommandHandlerLeaseTests" --no-restore -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter "SessionEndpointTests|EfSessionCommandServiceTests|EfSessionBillingIntegrationTests" --no-restore -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
```

Expected:

```text
Failed: 0
```

Commit:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Shared.Contracts src/AFK4.Platform.Api src/AFK4.Agent.Service tests/AFK4.Shared.Contracts.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "refactor: centralize device command names"
```

## Task 2: Session-Owned Heartbeat Command Planner

**Files:**

- Create: `D:\afk4.net\src\AFK4.Platform.Api\Sessions\HeartbeatSessionCommandPlan.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Sessions\IHeartbeatSessionCommandPlanner.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Sessions\EfHeartbeatSessionCommandPlanner.cs`
- Create: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\EfHeartbeatSessionCommandPlannerTests.cs`
- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Program.cs`

- [ ] **Step 1: Write failing planner tests**

Create `D:\afk4.net\tests\AFK4.Platform.Api.Tests\EfHeartbeatSessionCommandPlannerTests.cs` with tests for these exact cases:

```csharp
[Fact]
public async Task PlanAsync_ActiveSessionAndMissingAgentLease_ReturnsUnlockWithLease()

[Fact]
public async Task PlanAsync_ActiveSessionAndNearExpiryAgentLease_ReturnsRefreshLease()

[Fact]
public async Task PlanAsync_ActiveSessionAndHealthyAgentLease_ReturnsNoCommand()

[Fact]
public async Task PlanAsync_EndingSessionAndActiveAgentLease_ReturnsLock()

[Fact]
public async Task PlanAsync_NoCloudSessionAndActiveAgentLease_ReturnsLock()

[Fact]
public async Task PlanAsync_ExistingPendingSessionCommand_ReturnsNoDuplicate()
```

Use the existing `PlatformDbContext` in-memory test pattern from
`DeviceHeartbeatServicePersistenceTests`. Seed only the rows needed for each
case: organization, branch, device, seat, session, session lease, and pending
device command when testing duplicate suppression.

- [ ] **Step 2: Run planner tests and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter EfHeartbeatSessionCommandPlannerTests --no-restore -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
```

Expected:

```text
error CS0246: The type or namespace name 'IHeartbeatSessionCommandPlanner' could not be found
```

- [ ] **Step 3: Add planner contracts**

Create `D:\afk4.net\src\AFK4.Platform.Api\Sessions\HeartbeatSessionCommandPlan.cs`:

```csharp
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Sessions;

public sealed record HeartbeatSessionCommandPlan(
    Guid DeviceId,
    CreateDeviceCommandRequest Command);
```

Create `D:\afk4.net\src\AFK4.Platform.Api\Sessions\IHeartbeatSessionCommandPlanner.cs`:

```csharp
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Sessions;

public interface IHeartbeatSessionCommandPlanner
{
    Task<IReadOnlyList<HeartbeatSessionCommandPlan>> PlanAsync(
        Guid deviceId,
        DeviceHeartbeatRequest heartbeat,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Implement EF planner**

Create `D:\afk4.net\src\AFK4.Platform.Api\Sessions\EfHeartbeatSessionCommandPlanner.cs`.

Required behavior:

- find the newest non-ended session for `heartbeat.OrganizationId`,
  `heartbeat.BranchId`, and `deviceId` where `State` is `Active`, `Paused`, or
  `Ending`;
- if no session exists and `heartbeat.ActiveSessionId` is present, return a
  `lock` command with reason `heartbeat-session-missing`;
- if session state is `Ending`, return a `lock` command with reason
  `heartbeat-session-ending`;
- if session state is `Active` or `Paused` and the Agent has no matching active
  session, issue a new signed lease and return an `unlock` command with reason
  `heartbeat-session-continue`;
- if the Agent reports the matching session but
  `ActiveSessionLeaseExpiresAtUtc <= now + TimeSpan.FromMinutes(5)`, issue a
  new signed lease and return `refresh-session-lease` with reason
  `heartbeat-lease-refresh`;
- if a pending `lock`, `unlock`, or `refresh-session-lease` command already
  exists for the same device and session, return no duplicate command;
- when a new lease is issued, add a `SessionLeaseEntity`, update
  `SessionEntity.CurrentLeaseId`, and add a `session_events` row with event
  type `session-lease-refreshed-by-heartbeat`;
- never create sessions, extend `EndsAtUtc`, or append billing ledger entries.

- [ ] **Step 5: Register the planner**

Modify `D:\afk4.net\src\AFK4.Platform.Api\Program.cs`:

```csharp
builder.Services.AddScoped<IHeartbeatSessionCommandPlanner, EfHeartbeatSessionCommandPlanner>();
```

- [ ] **Step 6: Run planner tests and commit**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter EfHeartbeatSessionCommandPlannerTests --no-restore -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
```

Expected:

```text
Failed: 0
```

Commit:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: plan heartbeat session commands"
```

## Task 3: Heartbeat Service Integration

**Files:**

- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Devices\DeviceHeartbeatService.cs`
- Create: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\DeviceHeartbeatLeaseRefreshTests.cs`

- [ ] **Step 1: Write failing heartbeat integration tests**

Create `D:\afk4.net\tests\AFK4.Platform.Api.Tests\DeviceHeartbeatLeaseRefreshTests.cs` with tests for these cases:

```csharp
[Fact]
public async Task RecordHeartbeatAsync_ReturnsPlannedRefreshCommandWhenLeaseNearExpiry()

[Fact]
public async Task RecordHeartbeatAsync_ReturnsExistingPendingAndNewPlannedCommandsInCreatedOrder()

[Fact]
public async Task RecordHeartbeatAsync_DoesNotReturnDuplicateRefreshWhenPendingRefreshExists()
```

Seed with the same in-memory EF approach used by
`DeviceHeartbeatServicePersistenceTests`.

- [ ] **Step 2: Run integration tests and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter DeviceHeartbeatLeaseRefreshTests --no-restore -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
```

Expected:

```text
Failed: 1
```

The first failure should show that `DeviceHeartbeatService` returns only
pre-existing pending commands.

- [ ] **Step 3: Inject planner and command store**

Modify `DeviceHeartbeatService` constructor to accept:

```csharp
IHeartbeatSessionCommandPlanner heartbeatSessionCommandPlanner,
IDeviceCommandStore deviceCommandStore
```

After persisting device status and before loading pending commands:

1. Call `heartbeatSessionCommandPlanner.PlanAsync(deviceId, request, cancellationToken)`.
2. For each returned plan, call `deviceCommandStore.AddPendingAsync(plan.DeviceId, commandDto, cancellationToken)` using a new command id and `DateTimeOffset.UtcNow`.
3. Load pending commands from `dbContext.DeviceCommands` as the service does today.

Keep the final response shape unchanged:

```csharp
return new DeviceHeartbeatResponse(
    ServerTimeUtc: DateTimeOffset.UtcNow,
    HeartbeatIntervalSeconds: 10,
    Commands: commands);
```

- [ ] **Step 4: Run heartbeat and existing device tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter "DeviceHeartbeatLeaseRefreshTests|DeviceHeartbeatServicePersistenceTests|DeviceHeartbeatEndpointTests|SessionReconciliationEndpointTests" --no-restore -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
```

Expected:

```text
Failed: 0
```

- [ ] **Step 5: Commit**

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: refresh session leases from heartbeat"
```

## Task 4: Verification And Progress Update

**Files:**

- Modify: `D:\afk4.net\docs\progress\2026-05-12-vertical-slice-progress.md`

- [ ] **Step 1: Run full verification**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected:

```text
Build succeeded.
Failed: 0
```

- [ ] **Step 2: Update progress**

Add a short section to
`D:\afk4.net\docs\progress\2026-05-12-vertical-slice-progress.md` recording:

- the new heartbeat lease refresh planner;
- targeted test counts for planner, heartbeat, Agent command handling, and session command service;
- full build/test results;
- any remaining caveat around production lease duration tuning.

- [ ] **Step 3: Commit progress**

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add docs/progress/2026-05-12-vertical-slice-progress.md
& 'C:\Program Files\Git\cmd\git.exe' commit -m "docs: record heartbeat lease refresh follow-up"
```

## Plan Self-Review

Spec coverage:

- Cloud backend remains the session authority through the session-owned
  planner.
- Agent does not gain any local authorization or signing capability.
- Existing heartbeat lease snapshot fields are used instead of adding a new
  transport surface.
- Explicit operator-driven session actions remain in `EfSessionCommandService`.
- The plan avoids billing, POS, updates, reports, and Player Shell trust
  boundary changes.

Placeholder scan:

- No unresolved placeholder markers are present.
- Commands and expected outcomes are concrete.

Type consistency:

- Command names use `DeviceCommandTypeNames`.
- Planner output uses `HeartbeatSessionCommandPlan`.
- Heartbeat integration keeps returning `DeviceHeartbeatResponse`.
