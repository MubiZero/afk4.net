# AFK4 Phase 3 Club Layout And Device Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the Phase 3 backend and Operator foundations for persisted branch layout, device state visibility, installed app reporting, and technician-oriented device detail workflows.

**Architecture:** Keep AFK4 as the existing ASP.NET Core modular monolith with PostgreSQL as source of truth. Add persisted club operations data for zones and seats, keep devices separate from seats through explicit attachment records, replace the demo in-memory floor map with an EF-backed read service, and continue protecting operator-facing backend endpoints with staff bearer permissions.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, EF Core/Npgsql, EF Core InMemory tests, SignalR, WPF + MVVM, xUnit.

---

## Scope

Phase 3 from the PRD covers:

- persisted zones and seats;
- device enrollment, device credentials, and device state;
- command log and command status;
- installed apps reporting;
- device detail workflows.

Current gap analysis:

- Device enrollment, credentials, heartbeat state, command dispatch, and command status already exist with EF persistence and staff authorization.
- Operator App already has focused technician workflows for enrollment-code creation, command dispatch/status inspection, and credential rotation/revocation.
- The backend floor map still uses `InMemoryFloorMapReadService` with demo seat cards rather than persisted branch layout.
- There are no persisted `zones`, `seats`, or explicit seat/device attachment records.
- Installed apps reporting is not implemented.
- Device detail read endpoints and Operator App detail workflows are not implemented beyond command/credential actions.

This plan starts with the smallest strict Phase 3 slice: persisted zones/seats plus backend floor map from DB. It intentionally does not add a web admin panel or Operator sign-in UI.

## File Structure

Create and modify these files:

```text
D:\afk4.net\
  docs\superpowers\plans\2026-05-13-afk4-phase3-club-layout-device-management.md
  docs\progress\2026-05-12-vertical-slice-progress.md
  README.md
  src\AFK4.Shared.Contracts\
    FloorMap\SeatStatusDto.cs
    Identity\StaffPermissionNames.cs
  src\AFK4.Platform.Api\
    Data\PlatformDbContext.cs
    Data\ZoneEntity.cs
    Data\SeatEntity.cs
    Data\DeviceSeatAssignmentEntity.cs
    Data\Migrations\<timestamp>_AddClubLayout.cs
    FloorMap\EfFloorMapReadService.cs
    FloorMap\IFloorMapReadService.cs
    Identity\PermissionCatalog.cs
    Program.cs
  tests\AFK4.Shared.Contracts.Tests\
    ContractSerializationTests.cs
  tests\AFK4.Platform.Api.Tests\
    EfFloorMapReadServiceTests.cs
    FloorMapEndpointTests.cs
```

Responsibilities:

- `AFK4.Platform.Api.Data`: persisted club layout and current device-seat attachment records.
- `AFK4.Platform.Api.FloorMap`: branch floor map read model assembled from branches, zones, seats, attachments, and latest device heartbeat state.
- `AFK4.Shared.Contracts.FloorMap`: transport DTO consumed by Operator App and future device detail workflows.
- `AFK4.Shared.Contracts.Identity` and backend `PermissionCatalog`: stable permission names for floor map and later device detail reads.

## Task 1: Floor Map Contract For Persisted Layout

**Files:**

- Modify: `D:\afk4.net\src\AFK4.Shared.Contracts\FloorMap\SeatStatusDto.cs`
- Modify: `D:\afk4.net\tests\AFK4.Shared.Contracts.Tests\ContractSerializationTests.cs`

- [x] **Step 1: Write the failing contract test**

Add assertions to `FloorMapDto_ContainsSeatStatuses` that a seat can carry persisted layout and device attachment data:

```csharp
var seat = new SeatStatusDto(
    SeatId: Guid.Parse("e5edae8b-a833-4d92-ad8c-5864376d0414"),
    SeatName: "PC-001",
    ZoneId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
    ZoneName: "Main Hall",
    SortOrder: 10,
    State: "Locked",
    DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
    DeviceName: "PC-001",
    IsDeviceOnline: true,
    IsDeviceLocked: true,
    LastHeartbeatAtUtc: DateTimeOffset.Parse("2026-05-12T00:02:00Z"),
    AgentVersion: "0.1.1",
    ShellVersion: "0.1.2",
    ActiveSessionId: null,
    RemainingSeconds: null);
```

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter ContractSerializationTests --no-restore -p:UseSharedCompilation=false
```

Expected: compile failure because `SeatStatusDto` does not yet expose the persisted layout/device fields in this constructor shape.

- [x] **Step 2: Implement the expanded floor map seat contract**

Use this transport shape:

```csharp
namespace AFK4.Shared.Contracts.FloorMap;

public sealed record SeatStatusDto(
    Guid SeatId,
    string SeatName,
    Guid ZoneId,
    string ZoneName,
    int SortOrder,
    string State,
    Guid? DeviceId,
    string? DeviceName,
    bool? IsDeviceOnline,
    bool? IsDeviceLocked,
    DateTimeOffset? LastHeartbeatAtUtc,
    string? AgentVersion,
    string? ShellVersion,
    Guid? ActiveSessionId,
    int? RemainingSeconds);
```

- [x] **Step 3: Run the contract tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter ContractSerializationTests --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

## Task 2: Persisted Zones, Seats, And Seat Attachments

**Files:**

- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Data\PlatformDbContext.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\ZoneEntity.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\SeatEntity.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\DeviceSeatAssignmentEntity.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\Migrations\<timestamp>_AddClubLayout.cs`
- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Data\Migrations\PlatformDbContextModelSnapshot.cs`
- Create: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\EfFloorMapReadServiceTests.cs`

- [x] **Step 1: Write the failing EF floor map service test**

Create a test that seeds one organization, branch, zone, two seats, one enrolled device, and one active seat/device assignment. The expected floor map must return the branch name from DB, seats ordered by zone/seat sort order, and device state from `devices`.

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfFloorMapReadServiceTests --no-restore -p:UseSharedCompilation=false
```

Expected: compile failure because the persisted layout entities and EF floor map service do not exist.

- [x] **Step 2: Implement layout entities**

Create:

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class ZoneEntity
{
    public Guid ZoneId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
```

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class SeatEntity
{
    public Guid SeatId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid ZoneId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
```

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class DeviceSeatAssignmentEntity
{
    public Guid DeviceSeatAssignmentId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid SeatId { get; set; }
    public Guid DeviceId { get; set; }
    public DateTimeOffset AttachedAtUtc { get; set; }
    public DateTimeOffset? DetachedAtUtc { get; set; }
}
```

Add DbSets and table configuration:

- `zones`: key `ZoneId`, required `Name` length 120, index `{ OrganizationId, BranchId, SortOrder }`.
- `seats`: key `SeatId`, required `Name` length 80, index `{ OrganizationId, BranchId, ZoneId, SortOrder }`.
- `device_seat_assignments`: key `DeviceSeatAssignmentId`, indexes `{ SeatId, DetachedAtUtc }`, `{ DeviceId, DetachedAtUtc }`, and `{ OrganizationId, BranchId }`.

- [x] **Step 3: Add EF migration**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' ef migrations add AddClubLayout --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
```

Expected: migration creates `zones`, `seats`, and `device_seat_assignments`.

## Task 3: EF-Backed Floor Map Read Service And Protected Endpoint

**Files:**

- Modify: `D:\afk4.net\src\AFK4.Platform.Api\FloorMap\IFloorMapReadService.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\FloorMap\EfFloorMapReadService.cs`
- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Program.cs`
- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Identity\PermissionCatalog.cs`
- Modify: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\FloorMapEndpointTests.cs`

- [x] **Step 1: Write failing endpoint tests**

Update `FloorMapEndpointTests` to use `PlatformApiFactory`, seed a technician, seed a persisted branch layout, and verify:

- no bearer token returns `401`;
- a staff token without `floor_map.view` returns `403`;
- a technician or cashier with `floor_map.view` receives the persisted floor map.

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FloorMapEndpointTests --no-restore -p:UseSharedCompilation=false
```

Expected: failure because the current endpoint is anonymous and returns demo in-memory seats.

- [x] **Step 2: Implement async EF floor map reads**

Change `IFloorMapReadService` to:

```csharp
Task<FloorMapDto?> GetFloorMapAsync(Guid branchId, CancellationToken cancellationToken);
```

`EfFloorMapReadService` must:

- load the branch by `BranchId`;
- load seats with their zones for that branch;
- load active `DeviceSeatAssignmentEntity` rows where `DetachedAtUtc == null`;
- load latest `DeviceEntity` rows for assigned devices;
- return `"Maintenance"` when a seat has no device assignment;
- return `"Offline"` when an assigned device has `IsOnline == false`;
- return `"Locked"` when an assigned device is online and locked;
- return `"Free"` when an assigned device is online and unlocked.

- [x] **Step 3: Protect `GET /api/branches/{branchId}/floor-map`**

Use `StaffAuthorizationService.RequireBranchPermissionAsync(branchId, StaffPermissionNames.ViewFloorMap, cancellationToken)`.

Return:

- `401` when unauthenticated;
- `403` when authenticated but not authorized for the branch/permission, or
  when the branch is not available to the staff member's organization;
- `404` only if the authorized branch disappears before the read completes;
- `200` with `FloorMapDto` when allowed.

- [x] **Step 4: Run targeted tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter ContractSerializationTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "EfFloorMapReadServiceTests|FloorMapEndpointTests" --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

## Task 4: Installed Apps Reporting

**Files:**

- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Devices\InstalledAppReportRequest.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Devices\InstalledAppDto.cs`
- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Data\PlatformDbContext.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\DeviceInstalledAppEntity.cs`
- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Program.cs`
- Create: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\InstalledAppsEndpointTests.cs`

- [x] Add contract tests for installed app report serialization.
- [x] Add EF entity and migration for latest installed app snapshots per device.
- [x] Add authenticated device endpoint `POST /api/devices/{deviceId}/installed-apps/report`.
- [x] Reject route/device/credential mismatches.
- [x] Add staff-protected read path later through the device detail workflow task.

## Task 5: Device Detail Backend Workflow

**Files:**

- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Devices\DeviceDetailDto.cs`
- Modify: `D:\afk4.net\src\AFK4.Shared.Contracts\Identity\StaffPermissionNames.cs`
- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Identity\PermissionCatalog.cs`
- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Program.cs`
- Create: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\DeviceDetailEndpointTests.cs`

- [x] Add `devices.detail.view` permission.
- [x] Add `GET /api/devices/{deviceId}` returning device identity, branch, current assigned seat, latest heartbeat, active credential count, recent command statuses, and installed app count.
- [x] Protect the endpoint with staff bearer branch permission.
- [x] Add audit only for sensitive detail actions later; simple read does not write audit unless product scope changes.

## Task 6: Operator Device Detail Workflow

**Files:**

- Modify: `D:\afk4.net\src\AFK4.Operator.App\Devices\IOperatorDeviceApiClient.cs`
- Modify: `D:\afk4.net\src\AFK4.Operator.App\Devices\HttpOperatorDeviceApiClient.cs`
- Modify: `D:\afk4.net\src\AFK4.Operator.App\Devices\TechnicianDeviceWorkflowViewModel.cs`
- Modify: `D:\afk4.net\src\AFK4.Operator.App\MainWindow.xaml`
- Modify: `D:\afk4.net\tests\AFK4.Operator.App.Tests\OperatorDeviceApiClientTests.cs`
- Modify: `D:\afk4.net\tests\AFK4.Operator.App.Tests\TechnicianDeviceWorkflowViewModelTests.cs`

- [ ] Add typed client coverage for `GET /api/devices/{deviceId}`.
- [ ] Add ViewModel state for current device detail.
- [ ] Show device identity, assigned seat, online/lock state, Agent/Shell versions, active credentials, and recent command state in the existing dense technician panel.

## Task 7: Full Verification And Progress Log

**Files:**

- Modify: `D:\afk4.net\docs\progress\2026-05-12-vertical-slice-progress.md`
- Modify: `D:\afk4.net\README.md`

- [x] Run full build:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
```

- [x] Run full tests:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

- [x] Update progress log with implemented Phase 3 items, latest verification, and remaining gaps.

## Plan Self-Review

Spec coverage:

- PRD zones and seats are covered by Tasks 1-3.
- Device enrollment, credentials, state, command log, and command status are recognized as already implemented and reused by Tasks 3, 5, and 6.
- Installed apps reporting is covered by Task 4.
- Device detail workflows are covered by Tasks 5 and 6.
- No web admin panel, local club server, or Operator sign-in UI is added.

Placeholder scan:

- No `TBD`, `TODO`, or vague implementation markers remain.
- Deferred items are explicit roadmap tasks in this plan, not unresolved placeholders.

Type consistency:

- `SeatStatusDto` fields match the EF floor map read service expectations.
- `StaffPermissionNames.ViewFloorMap` already exists and is mapped for cashier, technician, supervisor, manager, and owner.
- Device state names match existing Operator App states: `Free`, `Locked`, and `Offline`, with `Maintenance` added for unassigned seats.
