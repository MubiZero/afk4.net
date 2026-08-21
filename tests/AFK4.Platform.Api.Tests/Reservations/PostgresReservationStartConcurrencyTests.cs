using System.Text.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Reservations;
using AFK4.Platform.Api.Sessions;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Platform.Api.Tests.Sessions;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AFK4.Platform.Api.Tests.Reservations;

public sealed class PostgresReservationStartConcurrencyTests
{
    private static readonly Guid ReservationId =
        Guid.Parse("a1111111-1111-4111-8111-111111111111");

    [PostgresSessionFact]
    public async Task DifferentKeysForOneReservation_CommitOneSessionAndLoserIsStableAndWinnerReplays()
    {
        await using var database = await CreateDatabaseAsync();
        await SeedReservationAsync(database);
        var gate = new TwoPartyStageGate();
        await using var firstDb = database.CreateDbContext();
        await using var secondDb = database.CreateDbContext();
        var firstDispatcher = new TrackingCommandDispatchService(firstDb, database.Now);
        var secondDispatcher = new TrackingCommandDispatchService(secondDb, database.Now);
        var firstLifecycle = new RecordingSessionLifecycleNotifier();
        var secondLifecycle = new RecordingSessionLifecycleNotifier();
        var firstCoordinator = CreateCoordinator(
            firstDb,
            gate.Wrap(CreateWorkflow(database, firstDb, firstDispatcher, firstLifecycle)),
            database.Now);
        var secondCoordinator = CreateCoordinator(
            secondDb,
            gate.Wrap(CreateWorkflow(database, secondDb, secondDispatcher, secondLifecycle)),
            database.Now);
        var firstRequest = ReservationRequest(database, "reservation-command-a");
        var secondRequest = ReservationRequest(database, "reservation-command-b");

        var results = await Task.WhenAll(
            firstCoordinator.StartAsync(
                ReservationId,
                database.StaffUserId,
                actorCanApproveComp: false,
                firstRequest,
                CancellationToken.None),
            secondCoordinator.StartAsync(
                ReservationId,
                database.StaffUserId,
                actorCanApproveComp: false,
                secondRequest,
                CancellationToken.None)).WaitAsync(TimeSpan.FromSeconds(30));

        var winner = Assert.Single(results, result => result.Succeeded);
        var loser = Assert.Single(results, result => !result.Succeeded);
        Assert.True(loser.Conflict);
        Assert.Equal("reservation_already_started", loser.Code);
        Assert.Equal(2, loser.CurrentVersion);

        var winningRequest = results[0].Succeeded ? firstRequest : secondRequest;
        await using var replayDb = database.CreateDbContext();
        var replayDispatcher = new TrackingCommandDispatchService(replayDb, database.Now);
        var replay = await CreateCoordinator(
                replayDb,
                CreateWorkflow(
                    database,
                    replayDb,
                    replayDispatcher,
                    new RecordingSessionLifecycleNotifier()),
                database.Now)
            .StartAsync(
                ReservationId,
                database.StaffUserId,
                actorCanApproveComp: false,
                winningRequest,
                CancellationToken.None);

        Assert.True(replay.Succeeded, replay.Error);
        Assert.Equal(
            winner.Response!.Session.Session.SessionId,
            replay.Response!.Session.Session.SessionId);
        Assert.Empty(replayDispatcher.Notifications);
        await AssertCommittedReservationStartAsync(
            database,
            winner.Response.Session.Session.SessionId,
            expectedAuditCount: 1);
        Assert.Equal(1, firstDispatcher.Notifications.Count + secondDispatcher.Notifications.Count);
        Assert.Equal(1, firstLifecycle.Events.Count + secondLifecycle.Events.Count);
    }

    [PostgresSessionFact]
    public async Task StartWhileConfirmationSaveIsHeld_IsRejectedThenConfirmedVersionStartsOnce()
    {
        await using var database = await CreateDatabaseAsync();
        await SeedReservationAsync(database, ReservationStateNames.Pending);
        var confirmGate = new ConfirmationSaveGateInterceptor();
        await using var confirmDb = database.CreateDbContext(confirmGate);
        var confirmService = new EfReservationService(
            confirmDb,
            new FixedTimeProvider(database.Now));
        var confirmTask = confirmService.ConfirmAsync(
            ReservationId,
            database.StaffUserId,
            new ConfirmReservationRequest(database.OrganizationId, ExpectedVersion: 1),
            CancellationToken.None);
        await confirmGate.WaitUntilSavingAsync();

        await using var earlyStartDb = database.CreateDbContext();
        var earlyDispatcher = new TrackingCommandDispatchService(earlyStartDb, database.Now);
        var earlyStart = await CreateCoordinator(
                earlyStartDb,
                CreateWorkflow(
                    database,
                    earlyStartDb,
                    earlyDispatcher,
                    new RecordingSessionLifecycleNotifier()),
                database.Now)
            .StartAsync(
                ReservationId,
                database.StaffUserId,
                actorCanApproveComp: false,
                ReservationRequest(database, "confirm-overlap-early"),
                CancellationToken.None);

        confirmGate.Release();
        var confirmed = await confirmTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(earlyStart.Conflict);
        Assert.Equal("reservation_confirmation_required", earlyStart.Code);
        Assert.Empty(earlyDispatcher.Notifications);
        Assert.True(confirmed.Succeeded, confirmed.Error);
        Assert.Equal(2, confirmed.Response!.Version);

        await using var committedStartDb = database.CreateDbContext();
        var committedDispatcher = new TrackingCommandDispatchService(committedStartDb, database.Now);
        var committedLifecycle = new RecordingSessionLifecycleNotifier();
        var committed = await CreateCoordinator(
                committedStartDb,
                CreateWorkflow(
                    database,
                    committedStartDb,
                    committedDispatcher,
                    committedLifecycle),
                database.Now)
            .StartAsync(
                ReservationId,
                database.StaffUserId,
                actorCanApproveComp: false,
                ReservationRequest(database, "confirm-overlap-committed") with
                {
                    ExpectedVersion = confirmed.Response.Version
                },
                CancellationToken.None);

        Assert.True(committed.Succeeded, committed.Error);
        await AssertCommittedReservationStartAsync(
            database,
            committed.Response!.Session.Session.SessionId,
            expectedAuditCount: 1,
            expectedReservationVersion: 3);
        Assert.Single(committedDispatcher.Notifications);
        Assert.Single(committedLifecycle.Events);
    }

    [PostgresSessionFact]
    public async Task ReservationAndNormalStartForSameSeat_CommitExactlyOneFinancialEffectSet()
    {
        await using var database = await CreateDatabaseAsync();
        await SeedReservationAsync(database);
        var gate = new TwoPartyStageGate();
        await using var reservationDb = database.CreateDbContext();
        await using var normalDb = database.CreateDbContext();
        var reservationDispatcher = new TrackingCommandDispatchService(reservationDb, database.Now);
        var normalDispatcher = new TrackingCommandDispatchService(normalDb, database.Now);
        var reservationLifecycle = new RecordingSessionLifecycleNotifier();
        var normalLifecycle = new RecordingSessionLifecycleNotifier();
        var reservationWorkflow = gate.Wrap(CreateWorkflow(
            database,
            reservationDb,
            reservationDispatcher,
            reservationLifecycle));
        var normalWorkflow = gate.Wrap(CreateWorkflow(
            database,
            normalDb,
            normalDispatcher,
            normalLifecycle));
        var coordinator = CreateCoordinator(reservationDb, reservationWorkflow, database.Now);
        var normalService = new EfSessionCommandService(
            normalDb,
            normalDispatcher,
            new FakeSessionLeaseSigner(),
            new FixedTimeProvider(database.Now),
            new TrackingSessionBillingService(
                normalDb,
                database.OrganizationId,
                database.BranchId),
            normalLifecycle,
            normalWorkflow);

        var reservationTask = coordinator.StartAsync(
            ReservationId,
            database.StaffUserId,
            actorCanApproveComp: false,
            ReservationRequest(database, "reservation-vs-normal"),
            CancellationToken.None);
        var normalTask = normalService.StartGuestSessionAsync(
            database.BranchId,
            database.StaffUserId,
            NormalRequest(database),
            SessionOriginNames.Operator,
            CancellationToken.None);
        await Task.WhenAll(reservationTask, normalTask).WaitAsync(TimeSpan.FromSeconds(30));

        var reservationResult = await reservationTask;
        var normalResult = await normalTask;
        Assert.NotEqual(reservationResult.Succeeded, normalResult.Succeeded);
        if (reservationResult.Succeeded)
        {
            Assert.True(normalResult.Conflict);
            Assert.Equal("seat_occupied", normalResult.Code);
        }
        else
        {
            Assert.True(reservationResult.Conflict);
            Assert.Equal("seat_unavailable", reservationResult.Code);
        }

        await using var verification = database.CreateDbContext();
        var session = await verification.Sessions.AsNoTracking().SingleAsync();
        var reservation = await verification.Reservations.AsNoTracking().SingleAsync();
        Assert.Single(await verification.SessionLeases.AsNoTracking().ToListAsync());
        Assert.Single(await verification.SessionEvents.AsNoTracking().ToListAsync());
        Assert.Single(await verification.LedgerEntries.AsNoTracking().ToListAsync());
        Assert.Single(await verification.DeviceCommands.AsNoTracking().ToListAsync());
        Assert.Single(await verification.SessionCommandIdempotency.AsNoTracking().ToListAsync());
        Assert.Equal(
            reservationResult.Succeeded ? 1 : 0,
            await verification.AuditRecords.AsNoTracking().CountAsync());
        if (reservationResult.Succeeded)
        {
            Assert.Equal(ReservationStateNames.Seated, reservation.State);
            Assert.Equal(2, reservation.Version);
            Assert.Equal(session.SessionId, reservation.StartedSessionId);
        }
        else
        {
            Assert.Equal(ReservationStateNames.Confirmed, reservation.State);
            Assert.Equal(1, reservation.Version);
            Assert.Null(reservation.StartedSessionId);
        }

        Assert.Equal(1, reservationDispatcher.Notifications.Count + normalDispatcher.Notifications.Count);
        Assert.Equal(1, reservationLifecycle.Events.Count + normalLifecycle.Events.Count);
    }

    private static Task<SessionStartPostgresFixture> CreateDatabaseAsync() =>
        SessionStartPostgresFixture.CreateAsync(
            Environment.GetEnvironmentVariable(PostgresSessionFactAttribute.EnvironmentVariable)!);

    private static EfReservationSessionCoordinator CreateCoordinator(
        PlatformDbContext db,
        ISessionStartWorkflow workflow,
        DateTimeOffset now) =>
        new(
            db,
            workflow,
            new AuditRecordStager(db, new FixedTimeProvider(now), new StaffContextAccessor()),
            new FixedTimeProvider(now));

    private static ISessionStartWorkflow CreateWorkflow(
        SessionStartPostgresFixture database,
        PlatformDbContext db,
        TrackingCommandDispatchService dispatcher,
        RecordingSessionLifecycleNotifier lifecycle) =>
        new EfSessionStartWorkflow(
            db,
            dispatcher,
            new FakeSessionLeaseSigner(),
            new FixedTimeProvider(database.Now),
            new TrackingSessionBillingService(db, database.OrganizationId, database.BranchId),
            lifecycle,
            new EfPlanLimitGuard(db));

    private static StartReservationSessionRequest ReservationRequest(
        SessionStartPostgresFixture database,
        string idempotencyKey) =>
        new(
            database.OrganizationId,
            ExpectedVersion: 1,
            TariffRuleVersionId: "reservation-postgres-v1",
            IdempotencyKey: idempotencyKey,
            DurationMode: SessionDurationModes.Fixed,
            DurationMinutes: 60,
            BillingMode: BillingModeNames.PrepaidWallet);

    private static StartGuestSessionRequest NormalRequest(SessionStartPostgresFixture database) =>
        new(
            database.OrganizationId,
            database.SeatId,
            TariffRuleVersionId: "normal-postgres-v1",
            IdempotencyKey: "normal-start-vs-reservation",
            DurationMode: SessionDurationModes.Fixed,
            DurationMinutes: 60,
            PlayerAccountId: database.PlayerAccountId,
            BillingMode: BillingModeNames.PrepaidWallet);

    private static async Task SeedReservationAsync(
        SessionStartPostgresFixture database,
        string state = ReservationStateNames.Confirmed)
    {
        await database.SeedAsync();
        await using var db = database.CreateDbContext();
        db.Reservations.Add(new ReservationEntity
        {
            ReservationId = ReservationId,
            OrganizationId = database.OrganizationId,
            BranchId = database.BranchId,
            PlayerAccountId = database.PlayerAccountId,
            SeatId = database.SeatId,
            CustomerName = "Reservation PostgreSQL Player",
            StartsAtUtc = database.Now.AddMinutes(-10),
            EndsAtUtc = database.Now.AddHours(1),
            State = state,
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

    private static async Task AssertCommittedReservationStartAsync(
        SessionStartPostgresFixture database,
        Guid sessionId,
        int expectedAuditCount,
        int expectedReservationVersion = 2)
    {
        await using var verification = database.CreateDbContext();
        var reservation = await verification.Reservations.AsNoTracking().SingleAsync();
        Assert.Equal(ReservationStateNames.Seated, reservation.State);
        Assert.Equal(expectedReservationVersion, reservation.Version);
        Assert.Equal(sessionId, reservation.StartedSessionId);
        Assert.Single(await verification.Sessions.AsNoTracking().ToListAsync());
        Assert.Single(await verification.SessionLeases.AsNoTracking().ToListAsync());
        Assert.Single(await verification.SessionEvents.AsNoTracking().ToListAsync());
        Assert.Single(await verification.LedgerEntries.AsNoTracking().ToListAsync());
        Assert.Single(await verification.DeviceCommands.AsNoTracking().ToListAsync());
        Assert.Single(await verification.SessionCommandIdempotency.AsNoTracking().ToListAsync());
        Assert.Equal(expectedAuditCount, await verification.AuditRecords.AsNoTracking().CountAsync());
    }

    private sealed class TwoPartyStageGate
    {
        private readonly TaskCompletionSource<bool> bothStaged = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;

        public ISessionStartWorkflow Wrap(ISessionStartWorkflow inner) => new GatedWorkflow(this, inner);

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
            private int calls;

            public async Task<SessionStartStage> StageAsync(
                Guid branchId,
                Guid actorStaffUserId,
                StartGuestSessionRequest request,
                bool actorCanApproveComp,
                string origin,
                CancellationToken cancellationToken)
            {
                var stage = await inner.StageAsync(
                    branchId,
                    actorStaffUserId,
                    request,
                    actorCanApproveComp,
                    origin,
                    cancellationToken);
                if (Interlocked.Increment(ref calls) == 1)
                {
                    await gate.WaitAsync();
                }

                return stage;
            }

            public Task NotifyCommittedAsync(SessionStartStage stage, CancellationToken cancellationToken) =>
                inner.NotifyCommittedAsync(stage, cancellationToken);
        }
    }

    private sealed class ConfirmationSaveGateInterceptor : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource<bool> saving = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> released = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitUntilSavingAsync() => saving.Task.WaitAsync(TimeSpan.FromSeconds(10));

        public void Release() => released.TrySetResult(true);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var confirmsReservation = eventData.Context?.ChangeTracker
                .Entries<ReservationEntity>()
                .Any(entry => entry.State == EntityState.Modified &&
                    entry.Entity.State == ReservationStateNames.Confirmed) == true;
            if (confirmsReservation)
            {
                saving.TrySetResult(true);
                await released.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }

            return result;
        }
    }

    private sealed class TrackingCommandDispatchService(
        PlatformDbContext db,
        DateTimeOffset now) : IDeviceCommandDispatchService
    {
        public List<(Guid DeviceId, DeviceCommandDto Command)> Notifications { get; } = [];

        public Task<DeviceCommandDto> EnqueueAsync(
            Guid deviceId,
            CreateDeviceCommandRequest request,
            CancellationToken cancellationToken)
        {
            var command = new DeviceCommandDto(Guid.NewGuid(), request.Type, now, request.Payload);
            db.DeviceCommands.Add(new DeviceCommandEntity
            {
                DeviceId = deviceId,
                CommandId = command.CommandId,
                Type = command.Type,
                PayloadJson = JsonSerializer.Serialize(command.Payload),
                Status = "pending",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            return Task.FromResult(command);
        }

        public Task NotifyAsync(
            Guid deviceId,
            DeviceCommandDto command,
            CancellationToken cancellationToken)
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

    private sealed class TrackingSessionBillingService(
        PlatformDbContext db,
        Guid organizationId,
        Guid branchId) : ISessionBillingService
    {
        public Task<SessionBillingValidationResult> ValidateStartAsync(
            Guid organizationId,
            Guid branchId,
            Guid? playerAccountId,
            string billingMode,
            Guid? tariffVersionId,
            Guid? playerPackageId,
            int durationMinutes,
            CancellationToken cancellationToken) =>
            Task.FromResult(Valid(durationMinutes));

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
            db.LedgerEntries.Add(new LedgerEntryEntity
            {
                LedgerEntryId = Guid.NewGuid(),
                OrganizationId = organizationId,
                BranchId = branchId,
                PlayerAccountId = playerAccountId,
                SessionId = sessionId,
                EntryType = LedgerEntryTypeNames.GameplayCharge,
                AccountType = LedgerAccountTypeNames.Wallet,
                AmountMinorUnits = -100,
                QuantitySeconds = validation.BillableSeconds,
                CurrencyCode = validation.CurrencyCode,
                Description = "Reservation concurrency proof",
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
            new(true, null, "postgres-v1", null, minutes * 60, 100, "TJS");
    }

    private sealed class RecordingSessionLifecycleNotifier : ISessionLifecycleNotifier
    {
        public List<SessionLifecycleChangedDto> Events { get; } = [];

        public Task NotifyAsync(SessionLifecycleChangedDto change, CancellationToken cancellationToken)
        {
            Events.Add(change);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSessionLeaseSigner : ISessionLeaseSigner
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
                $"reservation-concurrency-signature-{Sequence}");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
