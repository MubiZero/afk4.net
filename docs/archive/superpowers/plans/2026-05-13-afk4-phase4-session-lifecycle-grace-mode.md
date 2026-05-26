# AFK4 Phase 4 Session Lifecycle And Grace Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the first backend-authoritative session lifecycle and grace-mode foundation: start, extend, transfer, end, explicit states, signed leases, Agent lease validation, reconnect reconciliation, and session audit.

**Architecture:** Keep the backend as the ASP.NET Core modular monolith and PostgreSQL as the source of truth. The Sessions module owns session state, lease issuance, idempotent session commands, session events, and reconciliation; it uses the existing Device Management command dispatch path for lock, unlock, and lease-refresh commands. Agent Service validates backend-signed leases locally, but it cannot create or extend leases.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, EF Core/Npgsql, EF Core InMemory tests, SignalR device command path, WPF + MVVM client contracts, xUnit.

---

## Scope

This plan implements Phase 4 from the PRD and architecture source of truth:

- explicit session states: `requested`, `active`, `paused`, `ending`, `ended`, `failed`, `reconciled`;
- guest session start foundation with preserved tariff rule version text;
- session extend, transfer, and end foundations;
- idempotency for critical session commands;
- branch-scoped staff bearer permissions for operator session actions;
- audit records for allowed and denied critical session actions;
- backend-issued signed session leases;
- device unlock, lock, and lease-refresh commands through the existing `IDeviceCommandDispatchService`;
- Agent-side lease validation and current-lease storage;
- reconnect reconciliation through an authenticated device endpoint;
- floor-map active-session projection.

The plan intentionally does not add a web admin panel, a local club server, Operator sign-in UI, ledger charging, POS, tariff calculation, player account search, package consumption, real Windows lock enforcement, or Player Shell session UI. Those remain in their roadmap phases. This slice preserves the fields needed by those phases without making the Operator App or Agent the business authority.

## Current Baseline

Already available and reused:

- staff sign-in, refresh-token rotation, protected Operator token storage;
- branch-scoped staff authorization through `StaffAuthorizationService`;
- audit writer and existing device audit pattern;
- persisted organizations, branches, zones, seats, devices, device-seat assignments, device credentials, device command status, installed app snapshots;
- `IDeviceCommandDispatchService` and SignalR device command delivery;
- Agent realtime command handler that currently acknowledges commands;
- floor-map DTO already has `ActiveSessionId` and `RemainingSeconds`.

## File Structure

Create and modify these files:

```text
D:\afk4.net\
  docs\operations\local-postgres-smoke.md
  docs\progress\2026-05-12-vertical-slice-progress.md
  docs\superpowers\plans\2026-05-13-afk4-phase4-session-lifecycle-grace-mode.md
  README.md
  src\AFK4.Shared.Contracts\
    Devices\DeviceConnectionRequest.cs
    Devices\DeviceHeartbeatRequest.cs
    FloorMap\SeatStatusDto.cs
    Identity\StaffPermissionNames.cs
    Sessions\DeviceSessionSnapshotRequest.cs
    Sessions\SessionCommandResponse.cs
    Sessions\SessionDto.cs
    Sessions\SessionLeaseDto.cs
    Sessions\SessionLeasePayloadCanonicalizer.cs
    Sessions\SessionReconciliationResponse.cs
    Sessions\SessionStateNames.cs
    Sessions\StartGuestSessionRequest.cs
    Sessions\ExtendSessionRequest.cs
    Sessions\TransferSessionRequest.cs
    Sessions\EndSessionRequest.cs
  src\AFK4.Platform.Api\
    Audit\AuditActionNames.cs
    Data\PlatformDbContext.cs
    Data\SessionCommandIdempotencyEntity.cs
    Data\SessionEntity.cs
    Data\SessionEventEntity.cs
    Data\SessionLeaseEntity.cs
    Data\Migrations\<timestamp>_AddSessions.cs
    FloorMap\EfFloorMapReadService.cs
    Identity\PermissionCatalog.cs
    Program.cs
    Sessions\EcdsaSessionLeaseSigner.cs
    Sessions\EfSessionCommandService.cs
    Sessions\ISessionCommandService.cs
    Sessions\ISessionLeaseSigner.cs
    Sessions\SessionCommandIdempotencyKeyHasher.cs
    Sessions\SessionLeaseOptions.cs
    Sessions\SessionStateMachine.cs
  src\AFK4.Agent.Service\
    AgentOptions.cs
    DefaultDeviceCommandHandler.cs
    DeviceConnectionRequestFactory.cs
    HeartbeatPayloadFactory.cs
    ISessionLeaseStore.cs
    InMemorySessionLeaseStore.cs
    SessionLeaseValidationResult.cs
    SessionLeaseValidator.cs
    SessionReconciliationReporter.cs
    Worker.cs
  tests\AFK4.Shared.Contracts.Tests\
    SessionContractSerializationTests.cs
  tests\AFK4.Platform.Api.Tests\
    SessionEndpointTests.cs
    SessionLeaseSignerTests.cs
    SessionStateMachineTests.cs
    EfSessionCommandServiceTests.cs
    SessionReconciliationEndpointTests.cs
  tests\AFK4.Agent.Service.Tests\
    SessionLeaseValidatorTests.cs
    SessionCommandHandlerLeaseTests.cs
    SessionReconciliationReporterTests.cs
    HeartbeatPayloadFactoryTests.cs
```

Responsibilities:

- `AFK4.Shared.Contracts.Sessions`: transport contracts shared by backend, Agent, and Operator clients.
- `AFK4.Platform.Api.Sessions`: session state machine, command orchestration, idempotency, lease signing, and reconciliation application services.
- `AFK4.Platform.Api.Data`: EF-owned session tables and idempotency persistence.
- `AFK4.Agent.Service`: validates signed leases, tracks the current active lease, includes active lease state in heartbeat/reconnect reports, and rejects invalid session commands.

## Session Rules

State names are lowercase strings in contracts and persistence:

```text
requested
active
paused
ending
ended
failed
reconciled
```

Allowed transitions for this slice:

```text
requested -> active
requested -> failed
active -> active      // extend or transfer
active -> paused      // persisted state support; no Operator pause workflow in this slice
paused -> active
active -> ending
paused -> ending
ending -> ended
ending -> failed
ending -> reconciled
failed -> reconciled
```

Rejected transitions:

```text
ended -> active
ended -> ending
reconciled -> active
reconciled -> ending
failed -> active
```

Business rules:

- start requires an authenticated staff token with branch permission `sessions.start`;
- extend requires `sessions.extend`;
- transfer requires `sessions.transfer`;
- end requires `sessions.end`;
- reconciliation uses device credential authentication, not staff bearer identity;
- start validates organization, branch, seat, active device-seat assignment, and no active/ending session on the seat or device;
- extend validates active or paused session state and a positive extension duration;
- transfer validates active session state, target seat, target device assignment, and no active/ending session on the target seat or device;
- end validates active or paused state and moves to `ending` before the lock command is dispatched;
- session command idempotency key reuse with the same request returns the original response;
- session command idempotency key reuse with a different request returns `409 Conflict`;
- leases are signed by backend private key and verified by Agent public key;
- Agent cannot issue, extend, or modify a lease;
- offline grace is only local continuation of the last valid active lease.

## Task 1: Shared Session Contracts

**Files:**

- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Sessions\SessionStateNames.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Sessions\SessionLeaseDto.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Sessions\SessionDto.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Sessions\SessionCommandResponse.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Sessions\StartGuestSessionRequest.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Sessions\ExtendSessionRequest.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Sessions\TransferSessionRequest.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Sessions\EndSessionRequest.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Sessions\DeviceSessionSnapshotRequest.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Sessions\SessionReconciliationResponse.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Sessions\SessionLeasePayloadCanonicalizer.cs`
- Modify: `D:\afk4.net\src\AFK4.Shared.Contracts\Devices\DeviceHeartbeatRequest.cs`
- Modify: `D:\afk4.net\src\AFK4.Shared.Contracts\Devices\DeviceConnectionRequest.cs`
- Create: `D:\afk4.net\tests\AFK4.Shared.Contracts.Tests\SessionContractSerializationTests.cs`

- [ ] **Step 1: Write failing session contract serialization tests**

Create `D:\afk4.net\tests\AFK4.Shared.Contracts.Tests\SessionContractSerializationTests.cs`:

```csharp
using System.Text.Json;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Shared.Contracts.Tests;

public sealed class SessionContractSerializationTests
{
    [Fact]
    public void SessionCommandResponse_RoundTripsSessionLeaseAndDeviceCommands()
    {
        var lease = CreateLease();
        var session = new SessionDto(
            SessionId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            State: SessionStateNames.Active,
            TariffRuleVersionId: "manual-v1",
            StartedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            EndsAtUtc: DateTimeOffset.Parse("2026-05-13T11:00:00Z"),
            EndedAtUtc: null,
            RemainingSeconds: 3600,
            CurrentLease: lease);
        var command = new DeviceCommandDto(
            CommandId: Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
            Type: "unlock",
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            Payload: new Dictionary<string, string>
            {
                ["sessionId"] = session.SessionId.ToString("D"),
                ["sessionLease"] = JsonSerializer.Serialize(lease)
            });
        var response = new SessionCommandResponse(
            IdempotencyKey: "start-seat-1-20260513-1000",
            Session: session,
            DeviceCommands: [command]);

        var json = JsonSerializer.Serialize(response);
        var copy = JsonSerializer.Deserialize<SessionCommandResponse>(json);

        Assert.NotNull(copy);
        Assert.Equal(SessionStateNames.Active, copy.Session.State);
        Assert.Equal("manual-v1", copy.Session.TariffRuleVersionId);
        Assert.Equal(lease.Signature, copy.Session.CurrentLease?.Signature);
        Assert.Single(copy.DeviceCommands);
        Assert.Equal("unlock", copy.DeviceCommands[0].Type);
    }

    [Fact]
    public void DeviceHeartbeatRequest_CanCarryActiveLeaseSnapshot()
    {
        var heartbeat = new DeviceHeartbeatRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName: "PC-001",
            AgentVersion: "0.1.0",
            ShellVersion: "0.1.0",
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-13T10:02:00Z"),
            IsLocked: false,
            ActiveSessionId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            ActiveSessionLeaseExpiresAtUtc: DateTimeOffset.Parse("2026-05-13T10:15:00Z"),
            ActiveSessionLeaseSequence: 1);

        var json = JsonSerializer.Serialize(heartbeat);
        var copy = JsonSerializer.Deserialize<DeviceHeartbeatRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(heartbeat.ActiveSessionId, copy.ActiveSessionId);
        Assert.Equal(1, copy.ActiveSessionLeaseSequence);
    }

    private static SessionLeaseDto CreateLease()
    {
        return new SessionLeaseDto(
            SessionId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            State: SessionStateNames.Active,
            Sequence: 1,
            IssuedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            ExpiresAtUtc: DateTimeOffset.Parse("2026-05-13T10:15:00Z"),
            SignatureAlgorithm: "ECDSA-P256-SHA256",
            Signature: "signed-payload");
    }
}
```

- [ ] **Step 2: Run the contract test and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter SessionContractSerializationTests --no-restore -p:UseSharedCompilation=false
```

Expected: compile failure because the `Sessions` namespace and heartbeat lease snapshot fields do not exist.

- [ ] **Step 3: Add shared session contracts**

Create `D:\afk4.net\src\AFK4.Shared.Contracts\Sessions\SessionStateNames.cs`:

```csharp
namespace AFK4.Shared.Contracts.Sessions;

public static class SessionStateNames
{
    public const string Requested = "requested";
    public const string Active = "active";
    public const string Paused = "paused";
    public const string Ending = "ending";
    public const string Ended = "ended";
    public const string Failed = "failed";
    public const string Reconciled = "reconciled";
}
```

Create `SessionLeaseDto`, `SessionDto`, and `SessionCommandResponse` with this shape:

```csharp
namespace AFK4.Shared.Contracts.Sessions;

public sealed record SessionLeaseDto(
    Guid SessionId,
    Guid OrganizationId,
    Guid BranchId,
    Guid SeatId,
    Guid DeviceId,
    string State,
    int Sequence,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string SignatureAlgorithm,
    string Signature);

public sealed record SessionDto(
    Guid SessionId,
    Guid OrganizationId,
    Guid BranchId,
    Guid SeatId,
    Guid DeviceId,
    string State,
    string TariffRuleVersionId,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? EndsAtUtc,
    DateTimeOffset? EndedAtUtc,
    int? RemainingSeconds,
    SessionLeaseDto? CurrentLease);

public sealed record SessionCommandResponse(
    string IdempotencyKey,
    SessionDto Session,
    IReadOnlyList<AFK4.Shared.Contracts.Devices.DeviceCommandDto> DeviceCommands);
```

Create request contracts:

```csharp
namespace AFK4.Shared.Contracts.Sessions;

public sealed record StartGuestSessionRequest(
    Guid OrganizationId,
    Guid SeatId,
    int DurationMinutes,
    string TariffRuleVersionId,
    string IdempotencyKey);

public sealed record ExtendSessionRequest(
    int AdditionalMinutes,
    string TariffRuleVersionId,
    string IdempotencyKey);

public sealed record TransferSessionRequest(
    Guid TargetSeatId,
    string IdempotencyKey);

public sealed record EndSessionRequest(
    string Reason,
    string IdempotencyKey);
```

Create device reconciliation contracts:

```csharp
namespace AFK4.Shared.Contracts.Sessions;

public sealed record DeviceSessionSnapshotRequest(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    Guid? ActiveSessionId,
    SessionLeaseDto? ActiveLease,
    bool IsLocked,
    int PendingLocalEventCount,
    DateTimeOffset ObservedAtUtc);

public sealed record SessionReconciliationResponse(
    string Action,
    string Reason,
    Guid? SessionId,
    SessionLeaseDto? Lease);
```

Create canonical payload helper:

```csharp
namespace AFK4.Shared.Contracts.Sessions;

public static class SessionLeasePayloadCanonicalizer
{
    public static string CreatePayload(SessionLeaseDto leaseWithoutSignature)
    {
        return string.Join(
            "|",
            leaseWithoutSignature.SessionId.ToString("D"),
            leaseWithoutSignature.OrganizationId.ToString("D"),
            leaseWithoutSignature.BranchId.ToString("D"),
            leaseWithoutSignature.SeatId.ToString("D"),
            leaseWithoutSignature.DeviceId.ToString("D"),
            leaseWithoutSignature.State,
            leaseWithoutSignature.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            leaseWithoutSignature.IssuedAtUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            leaseWithoutSignature.ExpiresAtUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            leaseWithoutSignature.SignatureAlgorithm);
    }
}
```

Modify `DeviceHeartbeatRequest` and `DeviceConnectionRequest` by appending nullable active-session snapshot fields after existing constructor parameters so existing callers can be updated explicitly:

```csharp
Guid? ActiveSessionId,
DateTimeOffset? ActiveSessionLeaseExpiresAtUtc,
int? ActiveSessionLeaseSequence
```

- [ ] **Step 4: Run the contract test and verify GREEN**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter SessionContractSerializationTests --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

## Task 2: Permissions, Audit Names, Session Entities, And State Machine

**Files:**

- Modify: `D:\afk4.net\src\AFK4.Shared.Contracts\Identity\StaffPermissionNames.cs`
- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Identity\PermissionCatalog.cs`
- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Audit\AuditActionNames.cs`
- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Data\PlatformDbContext.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\SessionEntity.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\SessionEventEntity.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\SessionLeaseEntity.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\SessionCommandIdempotencyEntity.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Sessions\SessionStateMachine.cs`
- Create: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\SessionStateMachineTests.cs`

- [ ] **Step 1: Write failing state-machine tests**

Create `D:\afk4.net\tests\AFK4.Platform.Api.Tests\SessionStateMachineTests.cs`:

```csharp
using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Platform.Api.Tests;

public sealed class SessionStateMachineTests
{
    [Theory]
    [InlineData(SessionStateNames.Requested, SessionStateNames.Active)]
    [InlineData(SessionStateNames.Requested, SessionStateNames.Failed)]
    [InlineData(SessionStateNames.Active, SessionStateNames.Active)]
    [InlineData(SessionStateNames.Active, SessionStateNames.Paused)]
    [InlineData(SessionStateNames.Paused, SessionStateNames.Active)]
    [InlineData(SessionStateNames.Active, SessionStateNames.Ending)]
    [InlineData(SessionStateNames.Paused, SessionStateNames.Ending)]
    [InlineData(SessionStateNames.Ending, SessionStateNames.Ended)]
    [InlineData(SessionStateNames.Ending, SessionStateNames.Failed)]
    [InlineData(SessionStateNames.Ending, SessionStateNames.Reconciled)]
    [InlineData(SessionStateNames.Failed, SessionStateNames.Reconciled)]
    public void CanTransition_AllowsApprovedTransitions(string from, string to)
    {
        Assert.True(SessionStateMachine.CanTransition(from, to));
    }

    [Theory]
    [InlineData(SessionStateNames.Ended, SessionStateNames.Active)]
    [InlineData(SessionStateNames.Reconciled, SessionStateNames.Active)]
    [InlineData(SessionStateNames.Failed, SessionStateNames.Active)]
    public void CanTransition_RejectsTerminalStateRestart(string from, string to)
    {
        Assert.False(SessionStateMachine.CanTransition(from, to));
    }
}
```

- [ ] **Step 2: Run state-machine tests and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter SessionStateMachineTests --no-restore -p:UseSharedCompilation=false
```

Expected: compile failure because `SessionStateMachine` does not exist.

- [ ] **Step 3: Add session permissions and audit action names**

Append these constants to `StaffPermissionNames`:

```csharp
public const string StartSession = "sessions.start";
public const string ExtendSession = "sessions.extend";
public const string TransferSession = "sessions.transfer";
public const string EndSession = "sessions.end";
public const string ViewSession = "sessions.view";
```

Map `StartSession`, `ExtendSession`, `TransferSession`, `EndSession`, and `ViewSession` to owner, branch manager, shift supervisor, and cashier/operator. Map only `ViewSession` to accountant/auditor. Do not map session operator actions to technician by default.

Append these constants to `AuditActionNames`:

```csharp
public const string StartSession = "sessions.start";
public const string ExtendSession = "sessions.extend";
public const string TransferSession = "sessions.transfer";
public const string EndSession = "sessions.end";
```

- [ ] **Step 4: Add session EF entities**

Create `SessionEntity`:

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class SessionEntity
{
    public Guid SessionId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid SeatId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid CreatedByStaffUserId { get; set; }
    public string PlayerKind { get; set; } = "guest";
    public Guid? PlayerAccountId { get; set; }
    public string TariffRuleVersionId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTimeOffset RequestedAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public DateTimeOffset? EndedAtUtc { get; set; }
    public Guid? CurrentLeaseId { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
```

Create `SessionEventEntity`, `SessionLeaseEntity`, and `SessionCommandIdempotencyEntity`:

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class SessionEventEntity
{
    public Guid SessionEventId { get; set; }
    public Guid SessionId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public Guid? ActorStaffUserId { get; set; }
    public Guid? DeviceId { get; set; }
    public string DetailsJson { get; set; } = "{}";
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class SessionLeaseEntity
{
    public Guid SessionLeaseId { get; set; }
    public Guid SessionId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid SeatId { get; set; }
    public Guid DeviceId { get; set; }
    public string State { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public string SignatureAlgorithm { get; set; } = "ECDSA-P256-SHA256";
    public string Signature { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTimeOffset? RevokedAtUtc { get; set; }
}

public sealed class SessionCommandIdempotencyEntity
{
    public Guid SessionCommandIdempotencyId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public string IdempotencyKeyHash { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string ResponseJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
```

Add DbSets and model configuration in `PlatformDbContext`:

```csharp
public DbSet<SessionEntity> Sessions => Set<SessionEntity>();
public DbSet<SessionEventEntity> SessionEvents => Set<SessionEventEntity>();
public DbSet<SessionLeaseEntity> SessionLeases => Set<SessionLeaseEntity>();
public DbSet<SessionCommandIdempotencyEntity> SessionCommandIdempotency => Set<SessionCommandIdempotencyEntity>();
```

Use tables `sessions`, `session_events`, `session_leases`, and `session_command_idempotency`. Add indexes:

```csharp
entity.HasIndex(session => new { session.OrganizationId, session.BranchId, session.SeatId, session.State });
entity.HasIndex(session => new { session.OrganizationId, session.BranchId, session.DeviceId, session.State });
entity.HasIndex(session => session.CurrentLeaseId);
entity.HasIndex(sessionEvent => new { sessionEvent.SessionId, sessionEvent.CreatedAtUtc });
entity.HasIndex(lease => new { lease.SessionId, lease.Sequence }).IsUnique();
entity.HasIndex(lease => new { lease.DeviceId, lease.ExpiresAtUtc });
entity.HasIndex(record => new { record.OrganizationId, record.BranchId, record.IdempotencyKeyHash, record.Operation }).IsUnique();
```

- [ ] **Step 5: Implement state machine**

Create `D:\afk4.net\src\AFK4.Platform.Api\Sessions\SessionStateMachine.cs`:

```csharp
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Platform.Api.Sessions;

public static class SessionStateMachine
{
    private static readonly IReadOnlySet<(string From, string To)> AllowedTransitions =
        new HashSet<(string From, string To)>
        {
            (SessionStateNames.Requested, SessionStateNames.Active),
            (SessionStateNames.Requested, SessionStateNames.Failed),
            (SessionStateNames.Active, SessionStateNames.Active),
            (SessionStateNames.Active, SessionStateNames.Paused),
            (SessionStateNames.Paused, SessionStateNames.Active),
            (SessionStateNames.Active, SessionStateNames.Ending),
            (SessionStateNames.Paused, SessionStateNames.Ending),
            (SessionStateNames.Ending, SessionStateNames.Ended),
            (SessionStateNames.Ending, SessionStateNames.Failed),
            (SessionStateNames.Ending, SessionStateNames.Reconciled),
            (SessionStateNames.Failed, SessionStateNames.Reconciled)
        };

    public static bool CanTransition(string from, string to)
    {
        return AllowedTransitions.Contains((from, to));
    }
}
```

- [ ] **Step 6: Run state-machine tests and verify GREEN**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter SessionStateMachineTests --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

## Task 3: Backend Lease Signing

**Files:**

- Create: `D:\afk4.net\src\AFK4.Platform.Api\Sessions\SessionLeaseOptions.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Sessions\ISessionLeaseSigner.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Sessions\EcdsaSessionLeaseSigner.cs`
- Create: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\SessionLeaseSignerTests.cs`

- [ ] **Step 1: Write failing signer test**

Create `D:\afk4.net\tests\AFK4.Platform.Api.Tests\SessionLeaseSignerTests.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests;

public sealed class SessionLeaseSignerTests
{
    [Fact]
    public void Sign_CreatesVerifiableEcdsaSignature()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privatePem = key.ExportECPrivateKeyPem();
        var publicPem = key.ExportSubjectPublicKeyInfoPem();
        var signer = new EcdsaSessionLeaseSigner(Options.Create(new SessionLeaseOptions
        {
            SigningPrivateKeyPem = privatePem
        }));

        var lease = signer.Sign(
            SessionId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            OrganizationId: TestIds.OrganizationId,
            BranchId: TestIds.BranchId,
            SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
            DeviceId: TestIds.DeviceId,
            State: SessionStateNames.Active,
            Sequence: 1,
            IssuedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            ExpiresAtUtc: DateTimeOffset.Parse("2026-05-13T10:15:00Z"));

        using var verifier = ECDsa.Create();
        verifier.ImportFromPem(publicPem);
        var payload = SessionLeasePayloadCanonicalizer.CreatePayload(lease with { Signature = string.Empty });

        Assert.Equal("ECDSA-P256-SHA256", lease.SignatureAlgorithm);
        Assert.True(verifier.VerifyData(
            Encoding.UTF8.GetBytes(payload),
            Convert.FromBase64String(lease.Signature),
            HashAlgorithmName.SHA256));
    }
}
```

- [ ] **Step 2: Run signer test and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter SessionLeaseSignerTests --no-restore -p:UseSharedCompilation=false
```

Expected: compile failure because the signer types do not exist.

- [ ] **Step 3: Implement ECDSA lease signer**

Create:

```csharp
namespace AFK4.Platform.Api.Sessions;

public sealed class SessionLeaseOptions
{
    public string SigningPrivateKeyPem { get; init; } = string.Empty;
    public int LeaseMinutes { get; init; } = 15;
}

public interface ISessionLeaseSigner
{
    SessionLeaseDto Sign(
        Guid SessionId,
        Guid OrganizationId,
        Guid BranchId,
        Guid SeatId,
        Guid DeviceId,
        string State,
        int Sequence,
        DateTimeOffset IssuedAtUtc,
        DateTimeOffset ExpiresAtUtc);
}
```

Implement `EcdsaSessionLeaseSigner` using `ECDsa.ImportFromPem`, `SessionLeasePayloadCanonicalizer.CreatePayload`, `SignData(..., HashAlgorithmName.SHA256)`, and base64 signature output. Throw `InvalidOperationException` when `SigningPrivateKeyPem` is blank so production cannot silently issue unsigned leases.

- [ ] **Step 4: Run signer test and verify GREEN**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter SessionLeaseSignerTests --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

## Task 4: Idempotent Session Command Service

**Files:**

- Create: `D:\afk4.net\src\AFK4.Platform.Api\Sessions\SessionCommandIdempotencyKeyHasher.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Sessions\ISessionCommandService.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Sessions\EfSessionCommandService.cs`
- Create: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\EfSessionCommandServiceTests.cs`

- [ ] **Step 1: Write failing command service tests**

Create tests that verify:

- start creates an active session, a lease, a session event, an idempotency record, and an `unlock` device command;
- repeating start with the same idempotency key and same request returns the same response;
- repeating start with the same idempotency key and a different request returns a conflict result;
- end moves an active session to `ending` and dispatches a `lock` command;
- transfer dispatches `lock` to the old device and `unlock` to the new device.

Use a fake `IDeviceCommandDispatchService`:

```csharp
private sealed class RecordingCommandDispatchService : IDeviceCommandDispatchService
{
    public List<(Guid DeviceId, CreateDeviceCommandRequest Request)> Calls { get; } = [];

    public Task<DeviceCommandDto> DispatchAsync(Guid deviceId, CreateDeviceCommandRequest request, CancellationToken cancellationToken)
    {
        Calls.Add((deviceId, request));
        return Task.FromResult(new DeviceCommandDto(
            CommandId: Guid.NewGuid(),
            Type: request.Type,
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            Payload: request.Payload));
    }
}
```

- [ ] **Step 2: Run service tests and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfSessionCommandServiceTests --no-restore -p:UseSharedCompilation=false
```

Expected: compile failure because the service types do not exist.

- [ ] **Step 3: Implement idempotency hasher**

Create `SessionCommandIdempotencyKeyHasher`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace AFK4.Platform.Api.Sessions;

public static class SessionCommandIdempotencyKeyHasher
{
    public static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()))).ToLowerInvariant();
    }
}
```

- [ ] **Step 4: Implement session command service contract**

Use this service result shape:

```csharp
namespace AFK4.Platform.Api.Sessions;

public sealed record SessionCommandServiceResult(
    bool Succeeded,
    bool Conflict,
    bool NotFound,
    string? Error,
    SessionCommandResponse? Response)
{
    public static SessionCommandServiceResult Ok(SessionCommandResponse response) => new(true, false, false, null, response);
    public static SessionCommandServiceResult RequestConflict(string error) => new(false, true, false, error, null);
    public static SessionCommandServiceResult Missing(string error) => new(false, false, true, error, null);
    public static SessionCommandServiceResult Invalid(string error) => new(false, false, false, error, null);
}

public interface ISessionCommandService
{
    Task<SessionCommandServiceResult> StartGuestSessionAsync(Guid branchId, Guid actorStaffUserId, StartGuestSessionRequest request, CancellationToken cancellationToken);
    Task<SessionCommandServiceResult> ExtendSessionAsync(Guid sessionId, Guid actorStaffUserId, ExtendSessionRequest request, CancellationToken cancellationToken);
    Task<SessionCommandServiceResult> TransferSessionAsync(Guid sessionId, Guid actorStaffUserId, TransferSessionRequest request, CancellationToken cancellationToken);
    Task<SessionCommandServiceResult> EndSessionAsync(Guid sessionId, Guid actorStaffUserId, EndSessionRequest request, CancellationToken cancellationToken);
}
```

- [ ] **Step 5: Implement `EfSessionCommandService` rules**

Implement:

- validate non-empty idempotency key;
- calculate request hash from canonical JSON;
- check existing idempotency row for organization, branch, operation, and key hash;
- return saved response when request hash matches;
- return conflict when request hash differs;
- load active device-seat assignment for start and transfer;
- reject start when seat has no active assigned device;
- reject start when `DurationMinutes <= 0`;
- reject extend when `AdditionalMinutes <= 0`;
- reject start/transfer when the seat or device already has a session in `active`, `paused`, or `ending`;
- issue a new lease sequence using `ISessionLeaseSigner`;
- persist session, lease, event, and idempotency response in one EF transaction;
- dispatch `unlock`, `lock`, and `refresh-session-lease` commands through `IDeviceCommandDispatchService`.

Use device command payload keys:

```csharp
new Dictionary<string, string>
{
    ["sessionId"] = session.SessionId.ToString("D"),
    ["sessionLease"] = JsonSerializer.Serialize(lease),
    ["reason"] = "session-start"
}
```

Use `lock` payload for end:

```csharp
new Dictionary<string, string>
{
    ["sessionId"] = session.SessionId.ToString("D"),
    ["reason"] = request.Reason.Trim()
}
```

- [ ] **Step 6: Run service tests and verify GREEN**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfSessionCommandServiceTests --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

## Task 5: Staff-Protected Session Endpoints And Audit

**Files:**

- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Program.cs`
- Create: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\SessionEndpointTests.cs`

- [ ] **Step 1: Write failing endpoint tests**

Create endpoint tests for:

- `POST /api/branches/{branchId}/sessions/start` without staff token returns `401`;
- cashier/operator with `sessions.start` starts a guest session and writes succeeded audit;
- technician without `sessions.start` returns `403` and writes denied audit;
- duplicate idempotent start returns the same session id and command id;
- idempotency key reuse with a different seat returns `409`;
- `POST /api/sessions/{sessionId}/extend` requires `sessions.extend`;
- `POST /api/sessions/{sessionId}/transfer` requires `sessions.transfer`;
- `POST /api/sessions/{sessionId}/end` requires `sessions.end`.

Use existing `StaffAuthTestHelper.AuthorizeAsAsync` and seed organization, branch, zone, seat, device, and active device-seat assignment.

- [ ] **Step 2: Run endpoint tests and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter SessionEndpointTests --no-restore -p:UseSharedCompilation=false
```

Expected: route `404` or compile failure because session endpoints are absent.

- [ ] **Step 3: Register session services**

In `Program.cs` add:

```csharp
builder.Services.Configure<SessionLeaseOptions>(builder.Configuration.GetSection("Sessions"));
builder.Services.AddScoped<ISessionLeaseSigner, EcdsaSessionLeaseSigner>();
builder.Services.AddScoped<ISessionCommandService, EfSessionCommandService>();
```

- [ ] **Step 4: Map start session endpoint**

Add:

```csharp
app.MapPost("/api/branches/{branchId:guid}/sessions/start", async (
    Guid branchId,
    StartGuestSessionRequest request,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    ISessionCommandService sessionCommandService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.StartSession,
        cancellationToken);

    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
            authorization.StaffContext!.OrganizationId,
            branchId,
            authorization.StaffContext.StaffUserId,
            AuditActionNames.StartSession,
            "Session",
            null,
            AuditOutcome.Denied,
            "PlatformApi",
            JsonSerializer.Serialize(new { request.SeatId, authorization.DenialReason })),
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await sessionCommandService.StartGuestSessionAsync(
        branchId,
        authorization.StaffContext!.StaffUserId,
        request,
        cancellationToken);

    if (result.Conflict)
    {
        return Results.Conflict(new { Error = result.Error });
    }

    if (!result.Succeeded)
    {
        return Results.BadRequest(new { Error = result.Error });
    }

    await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
        authorization.StaffContext.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.StartSession,
        "Session",
        result.Response!.Session.SessionId.ToString("D"),
        AuditOutcome.Succeeded,
        "PlatformApi",
        JsonSerializer.Serialize(new { request.SeatId, request.DurationMinutes })),
        cancellationToken);

    return Results.Ok(result.Response);
});
```

- [ ] **Step 5: Map extend, transfer, and end endpoints**

Use the same authorization/audit/result pattern:

```text
POST /api/sessions/{sessionId:guid}/extend   -> StaffPermissionNames.ExtendSession   -> AuditActionNames.ExtendSession
POST /api/sessions/{sessionId:guid}/transfer -> StaffPermissionNames.TransferSession -> AuditActionNames.TransferSession
POST /api/sessions/{sessionId:guid}/end      -> StaffPermissionNames.EndSession      -> AuditActionNames.EndSession
```

For session-id routes, load the session first to get `BranchId`; return `404` when absent, then authorize against the session branch.

- [ ] **Step 6: Run endpoint tests and verify GREEN**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter SessionEndpointTests --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

## Task 6: Floor Map Active Session Projection

**Files:**

- Modify: `D:\afk4.net\src\AFK4.Platform.Api\FloorMap\EfFloorMapReadService.cs`
- Modify: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\EfFloorMapReadServiceTests.cs`

- [ ] **Step 1: Write failing floor-map active session test**

Add a test that seeds an active session for a seat and expects:

```csharp
Assert.Equal(activeSessionId, seat.ActiveSessionId);
Assert.Equal(1800, seat.RemainingSeconds);
Assert.Equal("Active", seat.State);
```

Use a fixed `TimeProvider` or compare a bounded remaining range when keeping the current service constructor. Prefer injecting `TimeProvider` into `EfFloorMapReadService` for deterministic tests.

- [ ] **Step 2: Run floor-map test and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfFloorMapReadServiceTests --no-restore -p:UseSharedCompilation=false
```

Expected: failure because `ActiveSessionId` and `RemainingSeconds` are always null.

- [ ] **Step 3: Project active session into floor map**

Update `EfFloorMapReadService` to load sessions for branch seats where state is `active`, `paused`, or `ending`. For each seat:

```csharp
var remainingSeconds = activeSession.EndsAtUtc is null
    ? null
    : Math.Max(0, (int)(activeSession.EndsAtUtc.Value - timeProvider.GetUtcNow()).TotalSeconds);
```

Set floor-map seat state priority:

```text
ending  -> Ending
paused  -> Paused
active  -> Active
no session + no device -> Maintenance
no session + offline device -> Offline
no session + locked device -> Locked
no session + unlocked device -> Free
```

- [ ] **Step 4: Run floor-map tests and verify GREEN**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfFloorMapReadServiceTests --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

## Task 7: Agent Lease Validation And Command Handling

**Files:**

- Modify: `D:\afk4.net\src\AFK4.Agent.Service\AgentOptions.cs`
- Create: `D:\afk4.net\src\AFK4.Agent.Service\SessionLeaseValidationResult.cs`
- Create: `D:\afk4.net\src\AFK4.Agent.Service\SessionLeaseValidator.cs`
- Create: `D:\afk4.net\src\AFK4.Agent.Service\ISessionLeaseStore.cs`
- Create: `D:\afk4.net\src\AFK4.Agent.Service\InMemorySessionLeaseStore.cs`
- Modify: `D:\afk4.net\src\AFK4.Agent.Service\DefaultDeviceCommandHandler.cs`
- Modify: `D:\afk4.net\src\AFK4.Agent.Service\Program.cs`
- Create: `D:\afk4.net\tests\AFK4.Agent.Service.Tests\SessionLeaseValidatorTests.cs`
- Create: `D:\afk4.net\tests\AFK4.Agent.Service.Tests\SessionCommandHandlerLeaseTests.cs`

- [ ] **Step 1: Write failing lease validator tests**

Test cases:

- valid lease signed by private key verifies with public key;
- wrong device id is rejected;
- expired lease is rejected;
- modified signature is rejected.

- [ ] **Step 2: Run lease validator tests and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter SessionLeaseValidatorTests --no-restore -p:UseSharedCompilation=false
```

Expected: compile failure because Agent lease validator types do not exist.

- [ ] **Step 3: Implement Agent lease options and validator**

Append to `AgentOptions`:

```csharp
public string LeaseSigningPublicKeyPem { get; init; } = string.Empty;
```

Create:

```csharp
namespace AFK4.Agent.Service;

public sealed record SessionLeaseValidationResult(bool IsValid, string? Error)
{
    public static SessionLeaseValidationResult Valid() => new(true, null);
    public static SessionLeaseValidationResult Invalid(string error) => new(false, error);
}
```

Implement `SessionLeaseValidator` using `ECDsa.ImportFromPem`, `SessionLeasePayloadCanonicalizer.CreatePayload`, `VerifyData(..., HashAlgorithmName.SHA256)`, and checks that lease organization, branch, and device match `AgentOptions`.

- [ ] **Step 4: Implement lease store**

Create:

```csharp
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Agent.Service;

public interface ISessionLeaseStore
{
    SessionLeaseDto? Current { get; }
    void Save(SessionLeaseDto lease);
    void Clear(Guid? sessionId);
}

public sealed class InMemorySessionLeaseStore : ISessionLeaseStore
{
    public SessionLeaseDto? Current { get; private set; }
    public void Save(SessionLeaseDto lease) => Current = lease;
    public void Clear(Guid? sessionId)
    {
        if (sessionId is null || Current?.SessionId == sessionId)
        {
            Current = null;
        }
    }
}
```

- [ ] **Step 5: Update command handler**

Update `DefaultDeviceCommandHandler` so:

- `unlock` requires payload key `sessionLease`, validates it, stores it, and returns `Accepted`;
- `refresh-session-lease` requires and validates `sessionLease`, replaces the current lease, and returns `Accepted`;
- `lock` clears current lease for the payload `sessionId` and returns `Accepted`;
- invalid or missing lease returns `Rejected` with an actionable message.

- [ ] **Step 6: Register Agent services**

In `Program.cs`:

```csharp
builder.Services.AddSingleton<ISessionLeaseStore, InMemorySessionLeaseStore>();
builder.Services.AddSingleton<SessionLeaseValidator>();
```

- [ ] **Step 7: Run Agent lease tests and verify GREEN**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "SessionLeaseValidatorTests|SessionCommandHandlerLeaseTests" --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

## Task 8: Heartbeat Snapshot And Reconnect Reconciliation

**Files:**

- Modify: `D:\afk4.net\src\AFK4.Agent.Service\HeartbeatPayloadFactory.cs`
- Modify: `D:\afk4.net\src\AFK4.Agent.Service\DeviceConnectionRequestFactory.cs`
- Create: `D:\afk4.net\src\AFK4.Agent.Service\SessionReconciliationReporter.cs`
- Modify: `D:\afk4.net\src\AFK4.Agent.Service\Worker.cs`
- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Program.cs`
- Create: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\SessionReconciliationEndpointTests.cs`
- Modify: `D:\afk4.net\tests\AFK4.Agent.Service.Tests\HeartbeatPayloadFactoryTests.cs`
- Create: `D:\afk4.net\tests\AFK4.Agent.Service.Tests\SessionReconciliationReporterTests.cs`

- [ ] **Step 1: Write failing heartbeat snapshot test**

Update `HeartbeatPayloadFactoryTests` so a current lease in `ISessionLeaseStore` produces:

```csharp
Assert.Equal(lease.SessionId, request.ActiveSessionId);
Assert.Equal(lease.ExpiresAtUtc, request.ActiveSessionLeaseExpiresAtUtc);
Assert.Equal(lease.Sequence, request.ActiveSessionLeaseSequence);
```

- [ ] **Step 2: Write failing reconciliation endpoint tests**

Create tests for:

- missing device credential returns `401`;
- active cloud session with matching valid local lease returns action `continue`;
- local active lease for a cloud-ended session returns action `lock`;
- cloud active session with no local lease returns action `unlock` and a signed lease.

- [ ] **Step 3: Run reconciliation tests and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter SessionReconciliationEndpointTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "HeartbeatPayloadFactoryTests|SessionReconciliationReporterTests" --no-restore -p:UseSharedCompilation=false
```

Expected: compile or assertion failures because snapshot and reconciliation do not exist.

- [ ] **Step 4: Add authenticated device reconciliation endpoint**

Map:

```text
POST /api/devices/{deviceId:guid}/session-reconciliation
```

Validation:

- route `deviceId` must match request `DeviceId`;
- route, credential, organization, branch, and device identity must match;
- request `ObservedAtUtc` must not be default.

Response rules:

```text
cloud active session + matching local active lease -> action "continue"
cloud active session + no local active lease       -> action "unlock" with current signed lease
no cloud active session + local active lease       -> action "lock"
cloud ending session                               -> action "lock"
unknown local session                              -> action "lock"
```

When response action is `unlock`, dispatch an `unlock` device command through `IDeviceCommandDispatchService` with the signed lease. When response action is `lock`, dispatch a `lock` command. Persist a `SessionEventEntity` with event type `device-reconciled`.

- [ ] **Step 5: Add Agent reconciliation reporter**

Create `SessionReconciliationReporter` that posts `DeviceSessionSnapshotRequest` to `/api/devices/{deviceId}/session-reconciliation` with `X-AFK4-Device-Credential`. It uses `ISessionLeaseStore.Current` and returns the typed response.

Call it from `Worker` after realtime start and before the installed apps report. Log failures and continue heartbeat, matching the installed-app reporting failure behavior.

- [ ] **Step 6: Run reconciliation tests and verify GREEN**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter SessionReconciliationEndpointTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "HeartbeatPayloadFactoryTests|SessionReconciliationReporterTests" --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

## Task 9: EF Migration, Docs, And Full Verification

**Files:**

- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\Migrations\<timestamp>_AddSessions.cs`
- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Data\Migrations\PlatformDbContextModelSnapshot.cs`
- Modify: `D:\afk4.net\docs\operations\local-postgres-smoke.md`
- Modify: `D:\afk4.net\docs\progress\2026-05-12-vertical-slice-progress.md`
- Modify: `D:\afk4.net\README.md`

- [ ] **Step 1: Create EF migration**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' ef migrations add AddSessions --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
```

Expected:

```text
Done. To undo this action, use 'ef migrations remove'
```

- [ ] **Step 2: Review migration**

Verify the migration creates:

- `sessions`;
- `session_events`;
- `session_leases`;
- `session_command_idempotency`.

Verify indexes exist for:

- active session lookup by organization, branch, seat, state;
- active session lookup by organization, branch, device, state;
- session event history by session and timestamp;
- unique session lease sequence per session;
- unique idempotency record by organization, branch, operation, and idempotency key hash.

- [ ] **Step 3: Update local PostgreSQL smoke runbook**

Add a Phase 4 smoke path:

- set `Sessions__SigningPrivateKeyPem` and Agent `Agent__LeaseSigningPublicKeyPem` from a generated ECDSA key pair;
- sign in as cashier/operator or branch manager;
- enroll a smoke device and assign it to a seeded seat;
- start a guest session with idempotency key `smoke-start-001`;
- repeat start with the same idempotency key and confirm the same `sessionId`;
- extend the session with idempotency key `smoke-extend-001`;
- transfer the session to another assigned seat/device if the smoke setup seeds a second device;
- end the session with idempotency key `smoke-end-001`;
- report device reconciliation and confirm action `lock` or `continue` according to the current session state;
- inspect PostgreSQL rows in `sessions`, `session_leases`, `session_events`, `session_command_idempotency`, `device_commands`, and `audit_records`.

- [ ] **Step 4: Update README and progress**

Update `README.md` current endpoints with:

```text
POST /api/branches/{branchId}/sessions/start
POST /api/sessions/{sessionId}/extend
POST /api/sessions/{sessionId}/transfer
POST /api/sessions/{sessionId}/end
POST /api/devices/{deviceId}/session-reconciliation
```

Update progress with implemented Phase 4 items, latest verification commands, known limitations, and smoke status.

- [ ] **Step 5: Run targeted tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter SessionContractSerializationTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "SessionStateMachineTests|SessionLeaseSignerTests|EfSessionCommandServiceTests|SessionEndpointTests|SessionReconciliationEndpointTests|EfFloorMapReadServiceTests" --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "SessionLeaseValidatorTests|SessionCommandHandlerLeaseTests|HeartbeatPayloadFactoryTests|SessionReconciliationReporterTests" --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

- [ ] **Step 6: Run full verification**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Expected:

```text
Build succeeded.
Passed! - Failed: 0
```

- [ ] **Step 7: Commit coherent Phase 4 slice**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' status --short
& 'C:\Program Files\Git\cmd\git.exe' add docs src tests README.md
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add session lifecycle grace mode foundation"
```

Expected:

```text
[codex/phase4-session-lifecycle-grace-mode ...] feat: add session lifecycle grace mode foundation
```

## Plan Self-Review

Spec coverage:

- PRD Phase 4 start, extend, transfer, end is covered by Tasks 1, 4, and 5.
- Explicit states are covered by Tasks 1 and 2.
- Idempotent session commands are covered by Task 4 and endpoint coverage in Task 5.
- Staff bearer branch permissions are covered by Task 5.
- Session audit is covered by Task 5.
- Backend authority is preserved because only backend endpoints create sessions, change session state, and issue leases.
- Device lock/unlock commands use the existing `IDeviceCommandDispatchService` in Tasks 4 and 8.
- Signed leases are covered by Task 3.
- Agent lease validation is covered by Task 7.
- Reconnect reconciliation is covered by Task 8.
- Floor-map active session visibility is covered by Task 6.

Out-of-scope checks:

- No Operator sign-in UI is added.
- No web admin panel is added.
- No local club server is added.
- No billing ledger, POS, package, or tariff calculation behavior is added.
- No real Windows enforcement or Player Shell session UI is added in this phase.

Placeholder scan:

- The plan has concrete file paths, test names, endpoint routes, command names, state names, permission names, audit action names, migration expectations, and verification commands.
- Open product decisions are not introduced.
- The only preserved future-facing fields are `PlayerAccountId` and `TariffRuleVersionId`, both required by the architecture and PRD.

Type consistency:

- `SessionStateNames` values match persisted state strings and floor-map projection rules.
- `StaffPermissionNames` values match `PermissionCatalog` and endpoint authorization.
- `AuditActionNames` values match endpoint audit writes.
- `SessionLeaseDto` is the same contract signed by the backend and verified by the Agent.
- Device command payload keys are stable: `sessionId`, `sessionLease`, and `reason`.
