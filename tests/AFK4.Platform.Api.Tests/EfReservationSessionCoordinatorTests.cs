using System.Text.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Reservations;
using AFK4.Platform.Api.Sessions;
using AFK4.Platform.Api.Tests.Sessions;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AFK4.Platform.Api.Tests;

public sealed class EfReservationSessionCoordinatorTests
{
    private static readonly Guid ReservationId = Guid.Parse("81111111-1111-4111-8111-111111111111");
    private static readonly Guid SeatId = Guid.Parse("82222222-2222-4222-8222-222222222222");
    private static readonly Guid PlayerAccountId = Guid.Parse("83333333-3333-4333-8333-333333333333");
    private static readonly Guid ActorStaffUserId = Guid.Parse("84444444-4444-4444-8444-444444444444");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-14T14:00:00Z");

    [Fact]
    public async Task StartAsync_MissingReservation_ReturnsStableNotFound()
    {
        await using var db = CreateDbContext();
        var workflow = new NeverCalledWorkflow();
        var coordinator = CreateCoordinator(db, workflow);

        var result = await coordinator.StartAsync(
            ReservationId,
            ActorStaffUserId,
            actorCanApproveComp: false,
            Request(expectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.NotFound);
        Assert.Equal("reservation_not_found", result.Code);
        Assert.Equal(0, workflow.StageCalls);
    }

    [Theory]
    [InlineData(ReservationStateNames.Pending)]
    [InlineData(ReservationStateNames.Cancelled)]
    public async Task StartAsync_NotConfirmed_ReturnsConfirmationRequired(string state)
    {
        await using var db = CreateDbContext();
        await SeedReservationAsync(db, state: state);
        var workflow = new NeverCalledWorkflow();
        var coordinator = CreateCoordinator(db, workflow);

        var result = await coordinator.StartAsync(
            ReservationId,
            ActorStaffUserId,
            actorCanApproveComp: false,
            Request(expectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.Conflict);
        Assert.Equal("reservation_confirmation_required", result.Code);
        Assert.Equal(0, workflow.StageCalls);
    }

    [Fact]
    public async Task StartAsync_ExpiredReservation_ReturnsStableConflict()
    {
        await using var db = CreateDbContext();
        await SeedReservationAsync(db, endsAtUtc: Now);
        var workflow = new NeverCalledWorkflow();
        var coordinator = CreateCoordinator(db, workflow);

        var result = await coordinator.StartAsync(
            ReservationId,
            ActorStaffUserId,
            actorCanApproveComp: false,
            Request(expectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.Conflict);
        Assert.Equal("reservation_expired", result.Code);
        Assert.Equal(0, workflow.StageCalls);
    }

    [Fact]
    public async Task StartAsync_StaleVersion_ReturnsAuthoritativeVersion()
    {
        await using var db = CreateDbContext();
        await SeedReservationAsync(db, version: 4);
        var workflow = new NeverCalledWorkflow();
        var coordinator = CreateCoordinator(db, workflow);

        var result = await coordinator.StartAsync(
            ReservationId,
            ActorStaffUserId,
            actorCanApproveComp: false,
            Request(expectedVersion: 3),
            CancellationToken.None);

        Assert.True(result.Conflict);
        Assert.Equal("version_conflict", result.Code);
        Assert.Equal(4, result.CurrentVersion);
        Assert.Equal(0, workflow.StageCalls);
    }

    [Fact]
    public async Task StartAsync_StalePendingReservation_ReturnsVersionConflictBeforeStateConflict()
    {
        await using var db = CreateDbContext();
        await SeedReservationAsync(db, state: ReservationStateNames.Pending, version: 4);
        var workflow = new NeverCalledWorkflow();
        var coordinator = CreateCoordinator(db, workflow);

        var result = await coordinator.StartAsync(
            ReservationId,
            ActorStaffUserId,
            actorCanApproveComp: false,
            Request(expectedVersion: 3),
            CancellationToken.None);

        Assert.True(result.Conflict);
        Assert.Equal("version_conflict", result.Code);
        Assert.Equal(4, result.CurrentVersion);
        Assert.Equal(0, workflow.StageCalls);
    }

    [Fact]
    public async Task StartAsync_MissingSeat_ReturnsSeatUnavailable()
    {
        await using var db = CreateDbContext();
        await SeedReservationAsync(db, hasSeat: false);
        var workflow = new NeverCalledWorkflow();
        var coordinator = CreateCoordinator(db, workflow);

        var result = await coordinator.StartAsync(
            ReservationId,
            ActorStaffUserId,
            actorCanApproveComp: false,
            Request(expectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.Conflict);
        Assert.Equal("seat_unavailable", result.Code);
        Assert.Equal(0, workflow.StageCalls);
    }

    [Fact]
    public async Task StartAsync_AlreadyLinkedWithDifferentKey_ReturnsAlreadyStarted()
    {
        await using var db = CreateDbContext();
        await SeedReservationAsync(
            db,
            state: ReservationStateNames.Seated,
            startedSessionId: Guid.NewGuid());
        var workflow = new NeverCalledWorkflow();
        var coordinator = CreateCoordinator(db, workflow);

        var result = await coordinator.StartAsync(
            ReservationId,
            ActorStaffUserId,
            actorCanApproveComp: false,
            Request(expectedVersion: 2, idempotencyKey: "different-key"),
            CancellationToken.None);

        Assert.True(result.Conflict);
        Assert.Equal("reservation_already_started", result.Code);
        Assert.Equal(0, workflow.StageCalls);
    }

    [Theory]
    [InlineData(BillingModeNames.PrepaidWallet, false)]
    [InlineData(BillingModeNames.Package, true)]
    public async Task StartAsync_GuestReservationCannotUsePlayerBilling(
        string billingMode,
        bool includePackage)
    {
        await using var db = CreateDbContext();
        await SeedReservationAsync(db, hasPlayer: false);
        var workflow = new NeverCalledWorkflow();
        var coordinator = CreateCoordinator(db, workflow);
        var request = Request(expectedVersion: 1) with
        {
            BillingMode = billingMode,
            PlayerPackageId = includePackage ? Guid.NewGuid() : null
        };

        var result = await coordinator.StartAsync(
            ReservationId,
            ActorStaffUserId,
            actorCanApproveComp: false,
            request,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("player_required_for_wallet", result.Code);
        Assert.Equal(0, workflow.StageCalls);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData(BillingModeNames.Package, false)]
    [InlineData(BillingModeNames.PrepaidWallet, true)]
    public async Task StartAsync_LinkedPlayerRequiresConsistentBillingAndPackageChoice(
        string billingMode,
        bool includePackage)
    {
        await using var db = CreateDbContext();
        await SeedReservationAsync(db);
        var workflow = new NeverCalledWorkflow();
        var coordinator = CreateCoordinator(db, workflow);
        var request = Request(expectedVersion: 1) with
        {
            BillingMode = billingMode,
            PlayerPackageId = includePackage ? Guid.NewGuid() : null
        };

        var result = await coordinator.StartAsync(
            ReservationId,
            ActorStaffUserId,
            actorCanApproveComp: false,
            request,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("session_start_invalid", result.Code);
        Assert.Equal(0, workflow.StageCalls);
    }

    [Theory]
    [InlineData("Insufficient wallet balance.", "insufficient_funds")]
    [InlineData("An open shift is required.", "open_shift_required")]
    [InlineData("Player account was not found.", "player_required_for_wallet")]
    public async Task StartAsync_MapsSessionValidationToStableOperatorCode(
        string workflowError,
        string expectedCode)
    {
        await using var db = CreateDbContext();
        await SeedReservationAsync(db);
        var coordinator = CreateCoordinator(db, new InvalidWorkflow(workflowError));

        var result = await coordinator.StartAsync(
            ReservationId,
            ActorStaffUserId,
            actorCanApproveComp: false,
            Request(expectedVersion: 1),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(expectedCode, result.Code);
    }

    [Fact]
    public async Task StartAsync_SuccessStagesEveryEffectLinksReservationAndNotifiesAfterCommit()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAndReservationAsync(db);
        var dispatcher = new TrackingCommandDispatchService(db);
        var billing = new TrackingBillingService(db);
        var lifecycle = new RecordingSessionLifecycleNotifier();
        var workflow = new EfSessionStartWorkflow(
            db,
            dispatcher,
            new FakeLeaseSigner(),
            new FixedTimeProvider(Now),
            billing,
            lifecycle);
        var coordinator = CreateCoordinator(db, workflow);

        var result = await coordinator.StartAsync(
            ReservationId,
            ActorStaffUserId,
            actorCanApproveComp: false,
            Request(expectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal(ReservationStateNames.Seated, result.Response.Reservation.State);
        Assert.Equal(2, result.Response.Reservation.Version);
        Assert.Equal(result.Response.Session.Session.SessionId, result.Response.Reservation.StartedSessionId);

        var reservation = await db.Reservations.SingleAsync();
        Assert.Equal(ReservationStateNames.Seated, reservation.State);
        Assert.Equal(Now, reservation.SeatedAtUtc);
        Assert.Equal(2, reservation.Version);
        Assert.Equal(result.Response.Session.Session.SessionId, reservation.StartedSessionId);
        Assert.Single(await db.Sessions.ToListAsync());
        Assert.Single(await db.SessionLeases.ToListAsync());
        Assert.Single(await db.SessionEvents.ToListAsync());
        Assert.Single(await db.LedgerEntries.ToListAsync());
        Assert.Single(await db.DeviceCommands.ToListAsync());
        Assert.Single(await db.SessionCommandIdempotency.ToListAsync());
        var audit = await db.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.StartReservationSession, audit.Action);
        Assert.Equal("Reservation", audit.TargetType);
        Assert.Equal(ReservationId.ToString("D"), audit.TargetId);
        Assert.Equal(ActorStaffUserId, audit.ActorStaffUserId);
        Assert.Contains(result.Response.Session.Session.SessionId.ToString("D"), audit.DetailsJson);
        Assert.Single(dispatcher.Notifications);
        Assert.Single(lifecycle.Events);
    }

    [Fact]
    public async Task StartAsync_LinkedPlayerComp_StartsFreeSessionWithoutOrdinaryBillingLedger()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAndReservationAsync(db);
        var dispatcher = new TrackingCommandDispatchService(db);
        var billing = new TrackingBillingService(db);
        var lifecycle = new RecordingSessionLifecycleNotifier();
        var workflow = new EfSessionStartWorkflow(
            db,
            dispatcher,
            new FakeLeaseSigner(),
            new FixedTimeProvider(Now),
            billing,
            lifecycle);
        var coordinator = CreateCoordinator(db, workflow);
        var request = Request(expectedVersion: 1) with
        {
            BillingMode = string.Empty,
            TariffVersionId = Guid.NewGuid(),
            IsComp = true,
            CompReason = "approved loyalty compensation"
        };

        var result = await coordinator.StartAsync(
            ReservationId,
            ActorStaffUserId,
            actorCanApproveComp: true,
            request,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(0, billing.AppendStartCalls);
        Assert.Empty(await db.LedgerEntries.ToListAsync());
        var session = await db.Sessions.SingleAsync();
        Assert.True(session.IsComp);
        Assert.Equal(PlayerAccountId, session.PlayerAccountId);
        Assert.Single(await db.SessionLeases.ToListAsync());
        Assert.Single(await db.SessionEvents.ToListAsync());
        Assert.Single(await db.DeviceCommands.ToListAsync());
        Assert.Single(await db.SessionCommandIdempotency.ToListAsync());
        Assert.Equal(session.SessionId, (await db.Reservations.SingleAsync()).StartedSessionId);
    }

    [Fact]
    public async Task StartAsync_WorkflowValidationFailureLeavesConfirmedReservationAndNoEffects()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAndReservationAsync(db);
        var dispatcher = new TrackingCommandDispatchService(db);
        var billing = new TrackingBillingService(db) { ValidationError = "Package is depleted." };
        var lifecycle = new RecordingSessionLifecycleNotifier();
        var workflow = new EfSessionStartWorkflow(
            db,
            dispatcher,
            new FakeLeaseSigner(),
            new FixedTimeProvider(Now),
            billing,
            lifecycle);
        var coordinator = CreateCoordinator(db, workflow);

        var result = await coordinator.StartAsync(
            ReservationId,
            ActorStaffUserId,
            actorCanApproveComp: false,
            Request(expectedVersion: 1),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("session_start_invalid", result.Code);
        var reservation = await db.Reservations.AsNoTracking().SingleAsync();
        Assert.Equal(ReservationStateNames.Confirmed, reservation.State);
        Assert.Equal(1, reservation.Version);
        Assert.Null(reservation.StartedSessionId);
        Assert.Empty(await db.Sessions.ToListAsync());
        Assert.Empty(await db.SessionLeases.ToListAsync());
        Assert.Empty(await db.SessionEvents.ToListAsync());
        Assert.Empty(await db.LedgerEntries.ToListAsync());
        Assert.Empty(await db.DeviceCommands.ToListAsync());
        Assert.Empty(await db.SessionCommandIdempotency.ToListAsync());
        Assert.Empty(dispatcher.Notifications);
        Assert.Empty(lifecycle.Events);
    }

    [Fact]
    public async Task StartAsync_SameKeyReplayReturnsSameSessionWithoutDuplicateEffectsOrNotifications()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAndReservationAsync(db);
        var dispatcher = new TrackingCommandDispatchService(db);
        var billing = new TrackingBillingService(db);
        var lifecycle = new RecordingSessionLifecycleNotifier();
        var workflow = new EfSessionStartWorkflow(
            db,
            dispatcher,
            new FakeLeaseSigner(),
            new FixedTimeProvider(Now),
            billing,
            lifecycle);
        var coordinator = CreateCoordinator(db, workflow);
        var request = Request(expectedVersion: 1);

        var first = await coordinator.StartAsync(
            ReservationId,
            ActorStaffUserId,
            actorCanApproveComp: false,
            request,
            CancellationToken.None);
        var replay = await coordinator.StartAsync(
            ReservationId,
            ActorStaffUserId,
            actorCanApproveComp: false,
            request,
            CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Equal(first.Response!.Session.Session.SessionId, replay.Response!.Session.Session.SessionId);
        Assert.Single(await db.Sessions.ToListAsync());
        Assert.Single(await db.SessionLeases.ToListAsync());
        Assert.Single(await db.SessionEvents.ToListAsync());
        Assert.Single(await db.LedgerEntries.ToListAsync());
        Assert.Single(await db.DeviceCommands.ToListAsync());
        Assert.Single(await db.SessionCommandIdempotency.ToListAsync());
        Assert.Single(await db.AuditRecords.ToListAsync());
        Assert.Single(dispatcher.Notifications);
        Assert.Single(lifecycle.Events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartAsync_AuditSaveFailureOrCancellationLeavesNoStartEffects(bool cancel)
    {
        await using var db = CreateDbContext(new RejectReservationStartAuditInterceptor(cancel));
        await SeedLayoutAndReservationAsync(db);
        var dispatcher = new TrackingCommandDispatchService(db);
        var lifecycle = new RecordingSessionLifecycleNotifier();
        var workflow = new EfSessionStartWorkflow(
            db,
            dispatcher,
            new FakeLeaseSigner(),
            new FixedTimeProvider(Now),
            new TrackingBillingService(db),
            lifecycle);
        var coordinator = CreateCoordinator(db, workflow);

        var exception = await Record.ExceptionAsync(() => coordinator.StartAsync(
            ReservationId,
            ActorStaffUserId,
            actorCanApproveComp: false,
            Request(expectedVersion: 1),
            CancellationToken.None));

        Assert.IsType(cancel ? typeof(OperationCanceledException) : typeof(ForcedAuditFailureException), exception);
        Assert.Empty(dispatcher.Notifications);
        Assert.Empty(lifecycle.Events);

        db.ChangeTracker.Clear();
        var reservation = await db.Reservations.AsNoTracking().SingleAsync();
        Assert.Equal(ReservationStateNames.Confirmed, reservation.State);
        Assert.Equal(1, reservation.Version);
        Assert.Null(reservation.StartedSessionId);
        Assert.Empty(await db.Sessions.AsNoTracking().ToListAsync());
        Assert.Empty(await db.SessionLeases.AsNoTracking().ToListAsync());
        Assert.Empty(await db.SessionEvents.AsNoTracking().ToListAsync());
        Assert.Empty(await db.LedgerEntries.AsNoTracking().ToListAsync());
        Assert.Empty(await db.DeviceCommands.AsNoTracking().ToListAsync());
        Assert.Empty(await db.SessionCommandIdempotency.AsNoTracking().ToListAsync());
        Assert.Empty(await db.AuditRecords.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task StartAsync_SameKeyDifferentExpectedVersionAfterSuccess_ReturnsIdempotencyConflict()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAndReservationAsync(db);
        var dispatcher = new TrackingCommandDispatchService(db);
        var workflow = CreateRealWorkflow(db, dispatcher);
        var coordinator = CreateCoordinator(db, workflow);
        var request = Request(expectedVersion: 1);

        var first = await coordinator.StartAsync(
            ReservationId,
            ActorStaffUserId,
            actorCanApproveComp: false,
            request,
            CancellationToken.None);
        var conflict = await coordinator.StartAsync(
            ReservationId,
            ActorStaffUserId,
            actorCanApproveComp: false,
            request with { ExpectedVersion = 2 },
            CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(conflict.Conflict);
        Assert.Equal("idempotency_conflict", conflict.Code);
        Assert.Single(await db.Sessions.ToListAsync());
    }

    [Fact]
    public async Task StartAsync_SameKeyDifferentBillingAfterSuccess_ReturnsIdempotencyConflict()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAndReservationAsync(db);
        var dispatcher = new TrackingCommandDispatchService(db);
        var workflow = CreateRealWorkflow(db, dispatcher);
        var coordinator = CreateCoordinator(db, workflow);
        var request = Request(expectedVersion: 1);

        var first = await coordinator.StartAsync(
            ReservationId,
            ActorStaffUserId,
            actorCanApproveComp: false,
            request,
            CancellationToken.None);
        var conflict = await coordinator.StartAsync(
            ReservationId,
            ActorStaffUserId,
            actorCanApproveComp: false,
            request with { BillingMode = BillingModeNames.PostpaidDebt },
            CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(conflict.Conflict);
        Assert.Equal("idempotency_conflict", conflict.Code);
        Assert.Single(await db.Sessions.ToListAsync());
    }

    [Fact]
    public async Task StartAsync_DifferentKeyAfterActualSuccess_ReturnsAlreadyStarted()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAndReservationAsync(db);
        var dispatcher = new TrackingCommandDispatchService(db);
        var workflow = CreateRealWorkflow(db, dispatcher);
        var coordinator = CreateCoordinator(db, workflow);
        var request = Request(expectedVersion: 1);

        var first = await coordinator.StartAsync(
            ReservationId,
            ActorStaffUserId,
            actorCanApproveComp: false,
            request,
            CancellationToken.None);
        var conflict = await coordinator.StartAsync(
            ReservationId,
            ActorStaffUserId,
            actorCanApproveComp: false,
            request with { IdempotencyKey = "new-command-key", ExpectedVersion = 2 },
            CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(conflict.Conflict);
        Assert.Equal("reservation_already_started", conflict.Code);
        Assert.Single(await db.Sessions.ToListAsync());
    }

    [PostgresSessionFact]
    public async Task StartAsync_PostgresFailureAfterDependencySave_RollsBackReservationAndEverySessionEffect()
    {
        await using var database = await SessionStartPostgresFixture.CreateAsync(
            Environment.GetEnvironmentVariable(PostgresSessionFactAttribute.EnvironmentVariable)!);
        await database.SeedAsync();
        await SeedPostgresReservationAsync(database);
        await using var db = database.CreateDbContext();
        var dispatcher = new TrackingCommandDispatchService(db);
        var billing = new TrackingBillingService(
            db,
            database.OrganizationId,
            database.BranchId);
        var lifecycle = new RecordingSessionLifecycleNotifier();
        var inner = new EfSessionStartWorkflow(
            db,
            dispatcher,
            new FakeLeaseSigner(),
            new FixedTimeProvider(database.Now),
            billing,
            lifecycle);
        var failing = new FailAfterDependencySaveWorkflow(inner, db);
        var coordinator = new EfReservationSessionCoordinator(
            db,
            failing,
            new AuditRecordStager(db, new FixedTimeProvider(database.Now), new StaffContextAccessor()),
            new FixedTimeProvider(database.Now));

        var exception = await Assert.ThrowsAsync<ForcedCoordinatorFailureException>(() =>
            coordinator.StartAsync(
                ReservationId,
                database.StaffUserId,
                actorCanApproveComp: false,
                PostgresRequest(database.OrganizationId),
                CancellationToken.None));

        Assert.Equal("forced coordinator failure after dependency save", exception.Message);
        Assert.True(failing.DependencySaveObserved);
        Assert.Empty(dispatcher.Notifications);
        Assert.Empty(lifecycle.Events);
        await using var verification = database.CreateDbContext();
        var reservation = await verification.Reservations.AsNoTracking().SingleAsync();
        Assert.Equal(ReservationStateNames.Confirmed, reservation.State);
        Assert.Equal(1, reservation.Version);
        Assert.Null(reservation.StartedSessionId);
        Assert.Empty(await verification.Sessions.AsNoTracking().ToListAsync());
        Assert.Empty(await verification.SessionLeases.AsNoTracking().ToListAsync());
        Assert.Empty(await verification.SessionEvents.AsNoTracking().ToListAsync());
        Assert.Empty(await verification.LedgerEntries.AsNoTracking().ToListAsync());
        Assert.Empty(await verification.DeviceCommands.AsNoTracking().ToListAsync());
        Assert.Empty(await verification.SessionCommandIdempotency.AsNoTracking().ToListAsync());
        Assert.Empty(await verification.AuditRecords.AsNoTracking().ToListAsync());
    }

    [PostgresSessionFact]
    public async Task StartAsync_PostgresAuditFailureAfterSave_RollsBackEveryEffectAndAudit()
    {
        await using var database = await SessionStartPostgresFixture.CreateAsync(
            Environment.GetEnvironmentVariable(PostgresSessionFactAttribute.EnvironmentVariable)!);
        await database.SeedAsync();
        await SeedPostgresReservationAsync(database);

        foreach (var cancel in new[] { false, true })
        {
            await using var db = database.CreateDbContext(new RejectSavedReservationStartAuditInterceptor(cancel));
            var dispatcher = new TrackingCommandDispatchService(db);
            var lifecycle = new RecordingSessionLifecycleNotifier();
            var workflow = new EfSessionStartWorkflow(
                db,
                dispatcher,
                new FakeLeaseSigner(),
                new FixedTimeProvider(database.Now),
                new TrackingBillingService(db, database.OrganizationId, database.BranchId),
                lifecycle);
            var coordinator = new EfReservationSessionCoordinator(
                db,
                workflow,
                new AuditRecordStager(db, new FixedTimeProvider(database.Now), new StaffContextAccessor()),
                new FixedTimeProvider(database.Now));

            var exception = await Record.ExceptionAsync(() => coordinator.StartAsync(
                ReservationId,
                database.StaffUserId,
                actorCanApproveComp: false,
                PostgresRequest(database.OrganizationId),
                CancellationToken.None));

            Assert.IsType(cancel ? typeof(OperationCanceledException) : typeof(ForcedAuditFailureException), exception);
            Assert.Empty(dispatcher.Notifications);
            Assert.Empty(lifecycle.Events);

            await using var verification = database.CreateDbContext();
            var reservation = await verification.Reservations.AsNoTracking().SingleAsync();
            Assert.Equal(ReservationStateNames.Confirmed, reservation.State);
            Assert.Equal(1, reservation.Version);
            Assert.Null(reservation.StartedSessionId);
            Assert.Empty(await verification.Sessions.AsNoTracking().ToListAsync());
            Assert.Empty(await verification.SessionLeases.AsNoTracking().ToListAsync());
            Assert.Empty(await verification.SessionEvents.AsNoTracking().ToListAsync());
            Assert.Empty(await verification.LedgerEntries.AsNoTracking().ToListAsync());
            Assert.Empty(await verification.DeviceCommands.AsNoTracking().ToListAsync());
            Assert.Empty(await verification.SessionCommandIdempotency.AsNoTracking().ToListAsync());
            Assert.Empty(await verification.AuditRecords.AsNoTracking().ToListAsync());
        }
    }

    [PostgresSessionFact]
    public async Task StartAsync_PostgresConcurrentSameKey_CreatesExactlyOneSessionAndReplaysWinner()
    {
        await using var database = await SessionStartPostgresFixture.CreateAsync(
            Environment.GetEnvironmentVariable(PostgresSessionFactAttribute.EnvironmentVariable)!);
        await database.SeedAsync();
        await SeedPostgresReservationAsync(database);
        await using var firstDb = database.CreateDbContext();
        await using var secondDb = database.CreateDbContext();
        var firstDispatcher = new TrackingCommandDispatchService(firstDb);
        var secondDispatcher = new TrackingCommandDispatchService(secondDb);
        var firstLifecycle = new RecordingSessionLifecycleNotifier();
        var secondLifecycle = new RecordingSessionLifecycleNotifier();
        var gate = new TwoPartyStageGate();
        var firstWorkflow = gate.Wrap(new EfSessionStartWorkflow(
            firstDb,
            firstDispatcher,
            new FakeLeaseSigner(),
            new FixedTimeProvider(database.Now),
            new TrackingBillingService(firstDb, database.OrganizationId, database.BranchId),
            firstLifecycle));
        var secondWorkflow = gate.Wrap(new EfSessionStartWorkflow(
            secondDb,
            secondDispatcher,
            new FakeLeaseSigner(),
            new FixedTimeProvider(database.Now),
            new TrackingBillingService(secondDb, database.OrganizationId, database.BranchId),
            secondLifecycle));
        var firstCoordinator = new EfReservationSessionCoordinator(
            firstDb,
            firstWorkflow,
            new AuditRecordStager(firstDb, new FixedTimeProvider(database.Now), new StaffContextAccessor()),
            new FixedTimeProvider(database.Now));
        var secondCoordinator = new EfReservationSessionCoordinator(
            secondDb,
            secondWorkflow,
            new AuditRecordStager(secondDb, new FixedTimeProvider(database.Now), new StaffContextAccessor()),
            new FixedTimeProvider(database.Now));
        var request = PostgresRequest(database.OrganizationId);

        var firstTask = firstCoordinator.StartAsync(
            ReservationId,
            database.StaffUserId,
            actorCanApproveComp: false,
            request,
            CancellationToken.None);
        var secondTask = secondCoordinator.StartAsync(
            ReservationId,
            database.StaffUserId,
            actorCanApproveComp: false,
            request,
            CancellationToken.None);
        var results = await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.All(results, result => Assert.True(result.Succeeded, result.Error));
        Assert.Equal(
            results[0].Response!.Session.Session.SessionId,
            results[1].Response!.Session.Session.SessionId);
        await using var verification = database.CreateDbContext();
        var reservation = await verification.Reservations.AsNoTracking().SingleAsync();
        Assert.Equal(ReservationStateNames.Seated, reservation.State);
        Assert.Equal(2, reservation.Version);
        Assert.Equal(results[0].Response!.Session.Session.SessionId, reservation.StartedSessionId);
        Assert.Single(await verification.Sessions.AsNoTracking().ToListAsync());
        Assert.Single(await verification.SessionLeases.AsNoTracking().ToListAsync());
        Assert.Single(await verification.SessionEvents.AsNoTracking().ToListAsync());
        Assert.Single(await verification.LedgerEntries.AsNoTracking().ToListAsync());
        Assert.Single(await verification.DeviceCommands.AsNoTracking().ToListAsync());
        Assert.Single(await verification.SessionCommandIdempotency.AsNoTracking().ToListAsync());
        Assert.Single(await verification.AuditRecords.AsNoTracking().ToListAsync());
        Assert.Equal(1, firstDispatcher.Notifications.Count + secondDispatcher.Notifications.Count);
        Assert.Equal(1, firstLifecycle.Events.Count + secondLifecycle.Events.Count);
    }

    private static EfReservationSessionCoordinator CreateCoordinator(
        PlatformDbContext db,
        ISessionStartWorkflow workflow) =>
        new(db, workflow, new AuditRecordStager(db, new FixedTimeProvider(Now), new StaffContextAccessor()), new FixedTimeProvider(Now));

    private static ISessionStartWorkflow CreateRealWorkflow(
        PlatformDbContext db,
        TrackingCommandDispatchService dispatcher) =>
        new EfSessionStartWorkflow(
            db,
            dispatcher,
            new FakeLeaseSigner(),
            new FixedTimeProvider(Now),
            new TrackingBillingService(db),
            new RecordingSessionLifecycleNotifier());

    private static StartReservationSessionRequest Request(
        int expectedVersion,
        string idempotencyKey = "reservation-start-1") =>
        new(
            TestIds.OrganizationId,
            expectedVersion,
            TariffRuleVersionId: "manual-v1",
            IdempotencyKey: idempotencyKey,
            DurationMode: SessionDurationModes.Fixed,
            DurationMinutes: 60,
            BillingMode: BillingModeNames.PrepaidWallet);

    private static StartReservationSessionRequest PostgresRequest(Guid organizationId) =>
        Request(expectedVersion: 1) with { OrganizationId = organizationId };

    private static PlatformDbContext CreateDbContext(IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }

        return new PlatformDbContext(builder.Options);
    }

    private static async Task SeedReservationAsync(
        PlatformDbContext db,
        string state = ReservationStateNames.Confirmed,
        bool hasSeat = true,
        bool hasPlayer = true,
        DateTimeOffset? endsAtUtc = null,
        int version = 1,
        Guid? startedSessionId = null)
    {
        db.Reservations.Add(new ReservationEntity
        {
            ReservationId = ReservationId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PlayerAccountId = hasPlayer ? PlayerAccountId : null,
            SeatId = hasSeat ? SeatId : null,
            CustomerName = hasPlayer ? "Player" : "Guest",
            StartsAtUtc = Now.AddMinutes(-10),
            EndsAtUtc = endsAtUtc ?? Now.AddHours(1),
            State = state,
            Source = ReservationSourceNames.Operator,
            Note = string.Empty,
            CreatedByStaffUserId = ActorStaffUserId,
            UpdatedByStaffUserId = ActorStaffUserId,
            CreatedAtUtc = Now.AddHours(-1),
            UpdatedAtUtc = Now.AddMinutes(-10),
            CancelReason = string.Empty,
            Version = version,
            StartedSessionId = startedSessionId
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedLayoutAndReservationAsync(PlatformDbContext db)
    {
        var zoneId = Guid.Parse("85555555-5555-4555-8555-555555555555");
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = TestIds.OrganizationId,
            Name = "Atomic Reservation Org",
            CreatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = TestIds.BranchId,
            OrganizationId = TestIds.OrganizationId,
            Name = "Atomic Reservation Branch",
            CreatedAtUtc = Now
        });
        db.Zones.Add(new ZoneEntity
        {
            ZoneId = zoneId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Main Hall",
            SortOrder = 1,
            CreatedAtUtc = Now
        });
        db.Seats.Add(new SeatEntity
        {
            SeatId = SeatId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ZoneId = zoneId,
            Name = "PC-001",
            SortOrder = 1,
            CreatedAtUtc = Now
        });
        db.Devices.Add(new DeviceEntity
        {
            DeviceId = TestIds.DeviceId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            MachineName = "PC-001",
            DisplayName = "PC-001",
            EnrolledAtUtc = Now,
            IsOnline = true,
            IsLocked = true
        });
        db.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
        {
            DeviceSeatAssignmentId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            SeatId = SeatId,
            DeviceId = TestIds.DeviceId,
            AttachedAtUtc = Now
        });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerAccountId,
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = "Reservation Player",
            IsActive = true,
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        await SeedReservationAsync(db);
    }

    private static async Task SeedPostgresReservationAsync(SessionStartPostgresFixture database)
    {
        await using var db = database.CreateDbContext();
        db.Reservations.Add(new ReservationEntity
        {
            ReservationId = ReservationId,
            OrganizationId = database.OrganizationId,
            BranchId = database.BranchId,
            PlayerAccountId = database.PlayerAccountId,
            SeatId = database.SeatId,
            CustomerName = "PostgreSQL Reservation Player",
            StartsAtUtc = database.Now.AddMinutes(-10),
            EndsAtUtc = database.Now.AddHours(1),
            State = ReservationStateNames.Confirmed,
            Source = ReservationSourceNames.Operator,
            Note = string.Empty,
            CreatedByStaffUserId = database.StaffUserId,
            UpdatedByStaffUserId = database.StaffUserId,
            CreatedAtUtc = database.Now.AddHours(-1),
            UpdatedAtUtc = database.Now.AddMinutes(-10),
            CancelReason = string.Empty,
            Version = 1
        });
        await db.SaveChangesAsync();
    }

    private sealed class NeverCalledWorkflow : ISessionStartWorkflow
    {
        public int StageCalls { get; private set; }

        public Task<SessionStartStage> StageAsync(
            Guid branchId,
            Guid actorStaffUserId,
            StartGuestSessionRequest request,
            bool actorCanApproveComp,
            CancellationToken cancellationToken)
        {
            StageCalls++;
            throw new InvalidOperationException("Workflow must not be called for an invalid reservation.");
        }

        public Task NotifyCommittedAsync(SessionStartStage stage, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Notification must not be called for an invalid reservation.");
    }

    private sealed class RejectReservationStartAuditInterceptor(bool cancel) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var hasStartAudit = eventData.Context?.ChangeTracker.Entries<AuditRecordEntity>()
                .Any(entry => entry.State == EntityState.Added &&
                    entry.Entity.Action == AuditActionNames.StartReservationSession) == true;
            if (!hasStartAudit)
            {
                return base.SavingChangesAsync(eventData, result, cancellationToken);
            }

            return cancel
                ? throw new OperationCanceledException("forced audit cancellation")
                : throw new ForcedAuditFailureException();
        }
    }

    private sealed class RejectSavedReservationStartAuditInterceptor(bool cancel) : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            var savedStartAudit = eventData.Context?.ChangeTracker.Entries<AuditRecordEntity>()
                .Any(entry => entry.Entity.Action == AuditActionNames.StartReservationSession) == true;
            if (!savedStartAudit)
            {
                return base.SavedChangesAsync(eventData, result, cancellationToken);
            }

            return cancel
                ? throw new OperationCanceledException("forced audit cancellation after save")
                : throw new ForcedAuditFailureException();
        }
    }

    private sealed class ForcedAuditFailureException()
        : Exception("forced reservation start audit failure");

    private sealed class InvalidWorkflow(string error) : ISessionStartWorkflow
    {
        public Task<SessionStartStage> StageAsync(
            Guid branchId,
            Guid actorStaffUserId,
            StartGuestSessionRequest request,
            bool actorCanApproveComp,
            CancellationToken cancellationToken) =>
            Task.FromResult(new SessionStartStage(
                SessionCommandServiceResult.Invalid(error),
                null,
                null));

        public Task NotifyCommittedAsync(SessionStartStage stage, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Invalid start must not notify.");
    }

    private sealed class TrackingCommandDispatchService(PlatformDbContext db) : IDeviceCommandDispatchService
    {
        public List<(Guid DeviceId, DeviceCommandDto Command)> Notifications { get; } = [];

        public Task<DeviceCommandDto> EnqueueAsync(
            Guid deviceId,
            CreateDeviceCommandRequest request,
            CancellationToken cancellationToken)
        {
            var command = new DeviceCommandDto(Guid.NewGuid(), request.Type, Now, request.Payload);
            db.DeviceCommands.Add(new DeviceCommandEntity
            {
                DeviceId = deviceId,
                CommandId = command.CommandId,
                Type = command.Type,
                PayloadJson = JsonSerializer.Serialize(command.Payload),
                Status = "pending",
                CreatedAtUtc = command.CreatedAtUtc,
                UpdatedAtUtc = command.CreatedAtUtc
            });
            return Task.FromResult(command);
        }

        public Task NotifyAsync(Guid deviceId, DeviceCommandDto command, CancellationToken cancellationToken)
        {
            Notifications.Add((deviceId, command));
            return Task.CompletedTask;
        }

        public async Task<DeviceCommandDto> DispatchAsync(
            Guid deviceId,
            CreateDeviceCommandRequest request,
            CancellationToken cancellationToken)
        {
            var command = await EnqueueAsync(deviceId, request, cancellationToken);
            await NotifyAsync(deviceId, command, cancellationToken);
            return command;
        }
    }

    private sealed class TrackingBillingService(
        PlatformDbContext db,
        Guid? organizationId = null,
        Guid? branchId = null) : ISessionBillingService
    {
        public string? ValidationError { get; init; }

        public int AppendStartCalls { get; private set; }

        public Task<SessionBillingValidationResult> ValidateStartAsync(
            Guid organizationId,
            Guid branchId,
            Guid? playerAccountId,
            string billingMode,
            Guid? tariffVersionId,
            Guid? playerPackageId,
            int durationMinutes,
            CancellationToken cancellationToken) =>
            Task.FromResult(ValidationError is null
                ? Valid(durationMinutes)
                : new SessionBillingValidationResult(false, ValidationError, "", null, 0, 0, "TJS"));

        public Task AppendStartLedgerEntriesAsync(
            Guid sessionId,
            Guid actorStaffUserId,
            SessionBillingValidationResult validation,
            Guid playerAccountId,
            Guid? playerPackageId,
            string billingMode,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            AppendStartCalls++;
            db.LedgerEntries.Add(new LedgerEntryEntity
            {
                LedgerEntryId = Guid.NewGuid(),
                OrganizationId = organizationId ?? TestIds.OrganizationId,
                BranchId = branchId ?? TestIds.BranchId,
                PlayerAccountId = playerAccountId,
                SessionId = sessionId,
                EntryType = "session-charge",
                AccountType = "cash",
                AmountMinorUnits = -100,
                QuantitySeconds = validation.BillableSeconds,
                CurrencyCode = validation.CurrencyCode,
                Description = "Atomic reservation start charge",
                Reason = "session-start",
                CreatedByStaffUserId = actorStaffUserId,
                CreatedAtUtc = now
            });
            return Task.CompletedTask;
        }

        public Task<SessionBillingValidationResult> ComputeCompValueAsync(Guid organizationId, Guid branchId, Guid tariffVersionId, int durationMinutes, CancellationToken cancellationToken) => Task.FromResult(Valid(durationMinutes));
        public Task<SessionBillingValidationResult> ValidateExtendAsync(Guid organizationId, Guid branchId, Guid? playerAccountId, string billingMode, Guid? tariffVersionId, Guid? playerPackageId, int additionalMinutes, CancellationToken cancellationToken) => Task.FromResult(Valid(additionalMinutes));
        public Task AppendExtendLedgerEntriesAsync(Guid sessionId, Guid actorStaffUserId, SessionBillingValidationResult validation, Guid playerAccountId, Guid? playerPackageId, string billingMode, DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<SessionBillingValidationResult> ComputeCheckoutChargeAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult(Valid(0));
        public Task AppendCheckoutLedgerEntriesAsync(Guid sessionId, Guid actorStaffUserId, SessionBillingValidationResult validation, Guid playerAccountId, DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;

        private static SessionBillingValidationResult Valid(int minutes) =>
            new(true, null, "manual-v1", null, minutes * 60, 100, "TJS");
    }

    private sealed class FakeLeaseSigner : ISessionLeaseSigner
    {
        public SessionLeaseDto Sign(
            Guid SessionId,
            Guid OrganizationId,
            Guid BranchId,
            Guid SeatId,
            Guid DeviceId,
            string State,
            int Sequence,
            DateTimeOffset IssuedAtUtc,
            DateTimeOffset ExpiresAtUtc) =>
            new(
                SessionId,
                OrganizationId,
                BranchId,
                SeatId,
                DeviceId,
                State,
                Sequence,
                IssuedAtUtc,
                ExpiresAtUtc,
                EcdsaSessionLeaseSigner.SignatureAlgorithm,
                $"reservation-signature-{Sequence}");
    }

    private sealed class FailAfterDependencySaveWorkflow(
        ISessionStartWorkflow inner,
        PlatformDbContext db) : ISessionStartWorkflow
    {
        public bool DependencySaveObserved { get; private set; }

        public async Task<SessionStartStage> StageAsync(
            Guid branchId,
            Guid actorStaffUserId,
            StartGuestSessionRequest request,
            bool actorCanApproveComp,
            CancellationToken cancellationToken)
        {
            var stage = await inner.StageAsync(
                branchId,
                actorStaffUserId,
                request,
                actorCanApproveComp,
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            DependencySaveObserved = stage.Result.Succeeded &&
                await db.Sessions.AsNoTracking().AnyAsync(cancellationToken) &&
                await db.SessionLeases.AsNoTracking().AnyAsync(cancellationToken) &&
                await db.SessionEvents.AsNoTracking().AnyAsync(cancellationToken) &&
                await db.LedgerEntries.AsNoTracking().AnyAsync(cancellationToken) &&
                await db.DeviceCommands.AsNoTracking().AnyAsync(cancellationToken) &&
                await db.SessionCommandIdempotency.AsNoTracking().AnyAsync(cancellationToken);
            throw new ForcedCoordinatorFailureException();
        }

        public Task NotifyCommittedAsync(SessionStartStage stage, CancellationToken cancellationToken) =>
            inner.NotifyCommittedAsync(stage, cancellationToken);
    }

    private sealed class ForcedCoordinatorFailureException()
        : InvalidOperationException("forced coordinator failure after dependency save");

    private sealed class TwoPartyStageGate
    {
        private readonly TaskCompletionSource<bool> bothStaged = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;

        public ISessionStartWorkflow Wrap(ISessionStartWorkflow inner) =>
            new GatedWorkflow(this, inner);

        private async Task WaitAsync()
        {
            if (Interlocked.Increment(ref arrivals) == 2)
            {
                bothStaged.TrySetResult(true);
            }

            await bothStaged.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        private sealed class GatedWorkflow(
            TwoPartyStageGate gate,
            ISessionStartWorkflow inner) : ISessionStartWorkflow
        {
            private int stageCalls;

            public async Task<SessionStartStage> StageAsync(
                Guid branchId,
                Guid actorStaffUserId,
                StartGuestSessionRequest request,
                bool actorCanApproveComp,
                CancellationToken cancellationToken)
            {
                var stage = await inner.StageAsync(
                    branchId,
                    actorStaffUserId,
                    request,
                    actorCanApproveComp,
                    cancellationToken);
                if (Interlocked.Increment(ref stageCalls) == 1)
                {
                    await gate.WaitAsync();
                }

                return stage;
            }

            public Task NotifyCommittedAsync(SessionStartStage stage, CancellationToken cancellationToken) =>
                inner.NotifyCommittedAsync(stage, cancellationToken);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
