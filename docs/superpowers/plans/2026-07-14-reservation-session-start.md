# Reservation Session Start Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Confirm reservations explicitly and start exactly one backend-approved session from a confirmed reservation while preserving Reservations, Sessions, Billing, and device-command module boundaries.

**Architecture:** Add reservation optimistic concurrency and a durable `StartedSessionId` link. Extract a transaction-neutral Sessions start workflow from `EfSessionCommandService`; a reservation-session coordinator owns the serializable cross-module transaction, then emits device/realtime notifications only after commit.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core, PostgreSQL 16, xUnit, React/TypeScript, Bun test.

## Global Constraints

- Execute after the POS split-settlement plan on the same integration branch.
- `pending` must be confirmed before start; only `confirmed` can start.
- Reservation seat and player identity are authoritative; the start request cannot substitute another seat/player.
- Early start is allowed and bills from actual session start time; `now >= EndsAtUtc` returns `reservation_expired`.
- One reservation links to at most one session and one session to at most one reservation.
- A failed start leaves the reservation confirmed and persists no session, ledger, lease, event, idempotency, or device command.
- The old state-only `seat` endpoint remains compatible but Operator Web no longer uses it as session start.
- No reservation wallet hold, mobile tariff picker, or no-show job enters this plan.

---

### Task 1: Add Reservation Version And Session Link Contracts

**Files:**
- Modify: `src/AFK4.Platform.Api/Data/ReservationEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`
- Create: `src/AFK4.Platform.Api/Data/Migrations/20260714130000_VersionReservationsAndLinkSessions.cs`
- Create: `src/AFK4.Platform.Api/Data/Migrations/20260714130000_VersionReservationsAndLinkSessions.Designer.cs`
- Modify: `src/AFK4.Platform.Api/Data/Migrations/PlatformDbContextModelSnapshot.cs`
- Modify: `src/AFK4.Shared.Contracts/Reservations/ReservationDto.cs`
- Modify: `src/AFK4.Shared.Contracts/Reservations/ReservationRequests.cs`
- Create: `src/AFK4.Shared.Contracts/Reservations/StartReservationSessionRequest.cs`
- Create: `src/AFK4.Shared.Contracts/Reservations/StartReservationSessionResponse.cs`
- Modify: `tests/AFK4.Shared.Contracts.Tests/ReservationContractSerializationTests.cs`

**Interfaces:**
- Produces: `ReservationEntity.Version : int`, `ReservationEntity.StartedSessionId : Guid?`, required `ExpectedVersion` on mutations, and reservation-session start request/response.

- [ ] **Step 1: Write failing serialization tests**

```csharp
[Fact]
public void ReservationDto_RoundTripsVersionAndStartedSession()
{
    var copy = RoundTrip(new ReservationDto(
        reservationId, organizationId, branchId, playerId, seatId, "PC-1", "VIP",
        "Player", "+992...", starts, ends, 60, "confirmed", "operator", "", created, updated,
        null, "", null, Version: 3, StartedSessionId: sessionId));
    Assert.Equal(3, copy.Version);
    Assert.Equal(sessionId, copy.StartedSessionId);
}
```

Also round-trip `StartReservationSessionRequest` with fixed duration, wallet billing, tariff version, package null, expected version, and idempotency key.

- [ ] **Step 2: Run RED**

```bash
dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter 'FullyQualifiedName~ReservationContractSerializationTests' -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected: compile failure for missing properties/types.

- [ ] **Step 3: Define exact contracts**

Append to `ReservationDto`:

```csharp
int Version = 1,
Guid? StartedSessionId = null
```

Add `int ExpectedVersion` to `UpdateReservationRequest`, `ConfirmReservationRequest`, `SeatReservationRequest`, and `CancelReservationRequest`.

Create:

```csharp
public sealed record StartReservationSessionRequest(
    Guid OrganizationId,
    int ExpectedVersion,
    string TariffRuleVersionId,
    string IdempotencyKey,
    string DurationMode = SessionDurationModes.Open,
    int? DurationMinutes = null,
    string BillingMode = "",
    Guid? TariffVersionId = null,
    Guid? PlayerPackageId = null,
    bool IsComp = false,
    string? CompReason = null);

public sealed record StartReservationSessionResponse(
    ReservationDto Reservation,
    SessionCommandResponse Session);
```

- [ ] **Step 4: Map model and migration**

```csharp
public int Version { get; set; } = 1;
public Guid? StartedSessionId { get; set; }
```

Configure `Version` as concurrency token and a unique filtered index/FK for `StartedSessionId`. Generate migration:

```bash
dotnet ef migrations add VersionReservationsAndLinkSessions --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
```

- [ ] **Step 5: Project fields, run GREEN, and commit**

Update `EfReservationService.ProjectAsync` constructor mapping.

```bash
dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter 'FullyQualifiedName~ReservationContractSerializationTests' -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
git add src/AFK4.Platform.Api/Data src/AFK4.Shared.Contracts/Reservations tests/AFK4.Shared.Contracts.Tests/ReservationContractSerializationTests.cs src/AFK4.Platform.Api/Reservations/EfReservationService.cs
git diff --cached --check
git commit -m "feat(reservations): version and link session starts"
```

### Task 2: Enforce Reservation Optimistic Concurrency

**Files:**
- Modify: `src/AFK4.Platform.Api/Reservations/ReservationServiceResult.cs`
- Modify: `src/AFK4.Platform.Api/Reservations/EfReservationService.cs`
- Modify: `src/AFK4.Platform.Api/Endpoints/ReservationEndpoints.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/ReservationEndpointTests.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/EfReservationGroupServiceTests.cs`
- Modify: `src/AFK4.Operator.App.Web/src/operatorApiClients.ts`
- Modify: `src/AFK4.Operator.App.Web/src/BackendBookingWorkspace.tsx`

**Interfaces:**
- Produces: stable `version_conflict` with authoritative `currentVersion`; every successful mutation increments Version once.

- [ ] **Step 1: Write failing stale-version tests**

For update, confirm, cancel, and legacy seat, send `ExpectedVersion = current.Version - 1` and assert:

```csharp
Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
Assert.Equal("version_conflict", body.Code);
Assert.Equal(current.Version, body.CurrentVersion);
```

Assert the entity is unchanged.

- [ ] **Step 2: Run RED**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter 'FullyQualifiedName~ReservationEndpointTests' -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

- [ ] **Step 3: Add conflict result and guarded mutation helper**

Extend result with `Code` and `CurrentVersion`, then use:

```csharp
private static ReservationServiceResult<ReservationDto>? GuardVersion(ReservationEntity reservation, int expected)
    => reservation.Version == expected
        ? null
        : ReservationServiceResult<ReservationDto>.RequestConflict(
            "Reservation changed since it was loaded.", "version_conflict", reservation.Version);
```

Before mutation call the guard; on success increment `reservation.Version++`. Translate `DbUpdateConcurrencyException` to the same stable conflict after reloading current version.

- [ ] **Step 4: Update Operator requests**

Send `expectedVersion: item.version` on confirm/move/cancel/legacy seat. On conflict refresh reservations and keep the drawer open.

- [ ] **Step 5: Run GREEN and commit**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter 'FullyQualifiedName~ReservationEndpointTests|FullyQualifiedName~EfReservationGroupServiceTests' -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
git add src/AFK4.Platform.Api/Reservations src/AFK4.Platform.Api/Endpoints/ReservationEndpoints.cs tests/AFK4.Platform.Api.Tests/ReservationEndpointTests.cs tests/AFK4.Platform.Api.Tests/EfReservationGroupServiceTests.cs src/AFK4.Operator.App.Web/src/operatorApiClients.ts src/AFK4.Operator.App.Web/src/BackendBookingWorkspace.tsx
git diff --cached --check
git commit -m "fix(reservations): reject stale operator commands"
```

### Task 3: Extract A Transaction-Neutral Session Start Workflow

**Files:**
- Create: `src/AFK4.Platform.Api/Sessions/ISessionStartWorkflow.cs`
- Create: `src/AFK4.Platform.Api/Sessions/EfSessionStartWorkflow.cs`
- Modify: `src/AFK4.Platform.Api/Sessions/EfSessionCommandService.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Create: `tests/AFK4.Platform.Api.Tests/EfSessionStartWorkflowTests.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/EfSessionCommandServiceTests.cs`

**Interfaces:**
- Produces: transaction-neutral `StageAsync` and after-commit `NotifyCommittedAsync`.

- [ ] **Step 1: Write characterization and RED tests**

Retain all existing start tests. Add a workflow test that begins an explicit transaction, calls `StageAsync`, verifies changes are tracked but external realtime notification has not fired, rolls back, and confirms no persisted session/ledger/command.

- [ ] **Step 2: Define workflow records and interface**

```csharp
public sealed record SessionStartStage(
    SessionCommandServiceResult Result,
    Guid? DeviceId,
    DeviceCommandDto? Command);

public interface ISessionStartWorkflow
{
    Task<SessionStartStage> StageAsync(
        Guid branchId,
        Guid actorStaffUserId,
        StartGuestSessionRequest request,
        bool actorCanApproveComp,
        CancellationToken cancellationToken);

    Task NotifyCommittedAsync(SessionStartStage stage, CancellationToken cancellationToken);
}
```

- [ ] **Step 3: Move start-domain staging without changing behavior**

Move duration, comp, assignment, blocking-session, billing validation, session/lease/event/ledger, device command enqueue, response, and idempotency-record creation from the body of `StartGuestSessionAsync` into `StageAsync`. `StageAsync` must not begin/commit/rollback a transaction and must not notify.

- [ ] **Step 4: Make the existing service the transaction owner for normal starts**

`EfSessionCommandService.StartGuestSessionAsync` keeps existing replay preflight, opens serializable transaction, calls workflow, saves/commits, catches unique-seat races, then calls `NotifyCommittedAsync` only on success. All existing endpoint behavior remains unchanged.

- [ ] **Step 5: Run GREEN and commit**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter 'FullyQualifiedName~EfSessionStartWorkflowTests|FullyQualifiedName~EfSessionCommandServiceTests' -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
git add src/AFK4.Platform.Api/Sessions src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/EfSessionStartWorkflowTests.cs tests/AFK4.Platform.Api.Tests/EfSessionCommandServiceTests.cs
git diff --cached --check
git commit -m "refactor(sessions): extract staged start workflow"
```

### Task 4: Coordinate Atomic Reservation Session Start

**Files:**
- Create: `src/AFK4.Platform.Api/Reservations/IReservationSessionCoordinator.cs`
- Create: `src/AFK4.Platform.Api/Reservations/EfReservationSessionCoordinator.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Create: `tests/AFK4.Platform.Api.Tests/EfReservationSessionCoordinatorTests.cs`

**Interfaces:**
- Produces: `StartAsync(Guid reservationId, Guid actorStaffUserId, bool actorCanApproveComp, StartReservationSessionRequest request, CancellationToken)`.
- Consumes: `ISessionStartWorkflow`, `PlatformDbContext`, `TimeProvider`.

- [ ] **Step 1: Write RED state/identity tests**

Cover missing, pending, cancelled, expired, stale version, missing seat, already linked, and mismatched identity. Assert stable codes from the spec.

- [ ] **Step 2: Write RED atomic success/rollback/replay tests**

On success assert one active session, reservation `seated`, version increment, `StartedSessionId`, lease/event/command/billing effects, and one idempotency record. Make workflow validation fail and assert reservation remains confirmed with no effects. Repeat same key and assert the same session ID.

- [ ] **Step 3: Define result and interface**

```csharp
public sealed record ReservationSessionStartResult(
    bool Succeeded,
    bool Conflict,
    bool NotFound,
    string? Code,
    string? Error,
    int? CurrentVersion,
    StartReservationSessionResponse? Response);
```

- [ ] **Step 4: Implement the serializable coordinator**

Inside one retryable serializable transaction:

```csharp
var sessionRequest = new StartGuestSessionRequest(
    reservation.OrganizationId,
    reservation.SeatId.Value,
    request.TariffRuleVersionId,
    $"reservation:{reservation.ReservationId:D}:{request.IdempotencyKey}",
    request.DurationMode,
    request.DurationMinutes,
    reservation.PlayerAccountId,
    request.BillingMode,
    request.TariffVersionId,
    request.PlayerPackageId,
    request.IsComp,
    request.CompReason);
```

Call `StageAsync`, then set `State = seated`, `SeatedAtUtc = now`, `StartedSessionId = response.SessionId`, and increment Version. Save and commit. Notify only after commit. On PostgreSQL serialization/unique conflicts clear the tracker and replay/re-evaluate up to three attempts.

- [ ] **Step 5: Run GREEN and commit**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter 'FullyQualifiedName~EfReservationSessionCoordinatorTests|FullyQualifiedName~EfSessionCommandServiceTests' -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
git add src/AFK4.Platform.Api/Reservations src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/EfReservationSessionCoordinatorTests.cs
git diff --cached --check
git commit -m "feat(reservations): start linked sessions atomically"
```

### Task 5: Expose Endpoint And Audit

**Files:**
- Modify: `src/AFK4.Platform.Api/Endpoints/ReservationEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Audit/AuditActionNames.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/ReservationEndpointTests.cs`
- Modify: `src/AFK4.Operator.App.Web/src/operatorApiClients.ts`
- Modify: `src/AFK4.Operator.App.Web/src/operatorApiClients.test.ts`
- Modify: `src/AFK4.Operator.App.Web/src/apiErrors.ts`
- Modify: `packages/i18n/src/messages.ts`

**Interfaces:**
- Produces: `POST /api/reservations/{reservationId}/start-session` and `reservations.startSession` client method.

- [ ] **Step 1: Write endpoint RED tests**

Cover authentication, `sessions.start` plus reservation-management permission, tenant scope, expected version, success response, stable 400/404/409 codes, and audit outcome. The audit target is Reservation and details include resulting SessionId.

- [ ] **Step 2: Run RED**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter 'FullyQualifiedName~ReservationEndpointTests' -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

- [ ] **Step 3: Map endpoint and client**

Require both `reservations.manage` and `sessions.start`, call the coordinator, and translate its result. Add:

```ts
startSession: (reservationId: string, request: StartReservationSessionRequest) =>
  api.post<StartReservationSessionResponse, StartReservationSessionRequest>(
    `/api/reservations/${reservationId}/start-session`, request)
```

Map all spec codes to localized Operator messages, including early-start copy and `reservation_expired`.

- [ ] **Step 4: Run GREEN and commit**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter 'FullyQualifiedName~ReservationEndpointTests' -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
cd src/AFK4.Operator.App.Web && bun test src/operatorApiClients.test.ts && cd ../../..
git add src/AFK4.Platform.Api/Endpoints/ReservationEndpoints.cs src/AFK4.Platform.Api/Audit/AuditActionNames.cs tests/AFK4.Platform.Api.Tests/ReservationEndpointTests.cs src/AFK4.Operator.App.Web/src/operatorApiClients.ts src/AFK4.Operator.App.Web/src/operatorApiClients.test.ts src/AFK4.Operator.App.Web/src/apiErrors.ts packages/i18n/src/messages.ts
git diff --cached --check
git commit -m "feat(api): start sessions from confirmed reservations"
```

### Task 6: Reuse The Session Start Form In Booking

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/session/SessionStartForm.tsx`
- Create: `src/AFK4.Operator.App.Web/src/session/SessionStartForm.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/MapSidePanel.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/MapSidePanel.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/BackendBookingWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/booking/BookingDrawer.tsx`
- Create: `src/AFK4.Operator.App.Web/src/booking/BookingDrawer.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/booking/bookingModel.ts`
- Modify: `src/AFK4.Operator.App.Web/src/booking/bookingModel.test.ts`
- Modify: `src/AFK4.Operator.App.Web/src/styles/10-booking.css`

**Interfaces:**
- Produces: shared `SessionStartForm` yielding the existing billing-selection fields; booking start command uses reservation seat/player/version.

- [ ] **Step 1: Write failing state-action tests**

Assert pending detail shows Confirm but not Start; confirmed shows Start but not Confirm; seated/cancelled show neither. Assert successful start calls `reservations.startSession` once, then opens the linked seat and refreshes.

- [ ] **Step 2: Write form characterization tests**

Lock the current Map behavior for guest, wallet, package, postpaid, fixed/open duration, tariff, comp, disabled, and validation states before extraction.

- [ ] **Step 3: Run RED/characterization**

```bash
cd src/AFK4.Operator.App.Web
bun test src/MapSidePanel.test.tsx src/booking/bookingModel.test.ts src/booking/BookingDrawer.test.tsx
```

Expected: characterization passes; new booking-start assertions fail.

- [ ] **Step 4: Extract SessionStartForm**

Define:

```ts
export type SessionStartSelection = {
  tariffRuleVersionId: string;
  durationMode: string;
  durationMinutes: number | null;
  billingMode: string;
  tariffVersionId: string | null;
  playerPackageId: string | null;
  isComp: boolean;
  compReason: string | null;
};
```

Props include fixed seat, optional fixed linked client, reference-data loaders, disabled state, value, and `onChange`. Map wraps it with its existing start command; Booking fixes client/seat from the reservation and submits the new endpoint.

- [ ] **Step 5: Replace state-only seating action**

In booking detail rename the action to localized `Начать сессию`. Open `PanelModal` with `SessionStartForm`. Keep one idempotency key per open/submit gesture; ambiguous retry reuses it, explicit retry regenerates it.

- [ ] **Step 6: Run GREEN and commit**

```bash
cd src/AFK4.Operator.App.Web
bun test src/session/SessionStartForm.test.tsx src/MapSidePanel.test.tsx src/booking/bookingModel.test.ts src/booking/BookingDrawer.test.tsx
cd ../../..
git add src/AFK4.Operator.App.Web/src/session src/AFK4.Operator.App.Web/src/MapSidePanel.tsx src/AFK4.Operator.App.Web/src/MapSidePanel.test.tsx src/AFK4.Operator.App.Web/src/BackendBookingWorkspace.tsx src/AFK4.Operator.App.Web/src/booking src/AFK4.Operator.App.Web/src/styles/10-booking.css
git diff --cached --check
git commit -m "feat(operator-booking): start confirmed reservations"
```

### Task 7: Prove Reservation Start Concurrency And Complete Verification

**Files:**
- Create: `tests/AFK4.Platform.Api.Tests/Reservations/PostgresReservationStartConcurrencyTests.cs`
- Create or reuse: `tests/AFK4.Platform.Api.Tests/Reservations/ReservationStartPostgresFixture.cs`
- Modify: `docs/progress/2026-05-12-vertical-slice-progress.md`
- Create: `.superpowers/sdd/operator-commerce-booking-completion-report.md`

**Interfaces:**
- Produces: deterministic live-DB proof, fresh full verification evidence, and durable progress state.

- [ ] **Step 1: Add deterministic PostgreSQL overlap tests**

Cover two starts of one reservation and reservation start racing a normal session start for the same seat. Assert one session, one reservation link, one billing effect set, and stable loser code/replay.

- [ ] **Step 2: Run live PostgreSQL focused tests**

```bash
AFK4_RESERVATION_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=afk4_reservation_test;Username=postgres;Password=postgres' \
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter 'FullyQualifiedName~PostgresReservationStartConcurrencyTests|FullyQualifiedName~EfReservationSessionCoordinatorTests' -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

- [ ] **Step 3: Run full web verification**

```bash
cd src/AFK4.Operator.App.Web
bun test $(find src -name '*.test.ts' -o -name '*.test.tsx' | grep -v App.test)
bun test src/App.test.tsx
bun run build
cd ../../..
```

- [ ] **Step 4: Run full backend/contracts/build verification**

```bash
dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
AFK4_COMMERCE_TEST_POSTGRES='Host=127.0.0.1;Port=5432;Database=afk4_commerce_test;Username=postgres;Password=postgres' \
AFK4_POS_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=afk4_pos_test;Username=postgres;Password=postgres' \
AFK4_RESERVATION_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=afk4_reservation_test;Username=postgres;Password=postgres' \
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
dotnet build AFK4.sln -p:EnableWindowsTargeting=true -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
git diff --check
```

- [ ] **Step 5: Self-review, update durable docs, and commit evidence**

Record exact pass counts, DB images/versions, environment limitations, and Windows-only remaining checks. Keep progress compact.

```bash
git add docs/progress/2026-05-12-vertical-slice-progress.md .superpowers/sdd/operator-commerce-booking-completion-report.md tests/AFK4.Platform.Api.Tests/Reservations
git diff --cached --check
git commit -m "test(operator): verify commerce and booking completion"
```

- [ ] **Step 6: Run whole-branch review and publication gate**

Review `UI_BASE..HEAD` for Critical/Important findings, fix through RED/GREEN commits, rerun affected and full gates, inspect staged status/diffs, then push only because the user explicitly requested publication. Open a PR; do not merge until required CI is green on the latest head and Windows-only checks are complete.
