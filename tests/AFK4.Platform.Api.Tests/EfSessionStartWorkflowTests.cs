using System.Text.Json;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Sessions;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using AFK4.Platform.Api.Loyalty;

namespace AFK4.Platform.Api.Tests;

public sealed class EfSessionStartWorkflowTests
{
    private static readonly Guid SeatId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ZoneId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ActorStaffUserId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid PlayerAccountId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-13T10:00:00Z");

    [Fact]
    public async Task StageAsync_TracksAuthoritativeStartWithoutSavingOrNotifying_AndRollbackPersistsNothing()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db);
        var dispatcher = new TrackingCommandDispatchService(db);
        var billing = new TrackingSessionBillingService(db);
        var lifecycle = new RecordingSessionLifecycleNotifier();
        var workflow = new EfSessionStartWorkflow(
            db,
            dispatcher,
            new FakeSessionLeaseSigner(),
            new FixedTimeProvider(Now),
            billing,
            lifecycle,
            new EfPlanLimitGuard(db));
        var request = new StartGuestSessionRequest(
            TestIds.OrganizationId,
            SeatId,
            TariffRuleVersionId: "manual-v1",
            IdempotencyKey: "staged-start-1",
            DurationMode: SessionDurationModes.Fixed,
            DurationMinutes: 60,
            PlayerAccountId: PlayerAccountId,
            BillingMode: BillingModeNames.PrepaidWallet);

        await using var transaction = await db.Database.BeginTransactionAsync();
        var stage = await workflow.StageAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            request,
            actorCanApproveComp: false,
            SessionOriginNames.Operator,
            CancellationToken.None);

        Assert.True(stage.Result.Succeeded);
        Assert.Equal(TestIds.DeviceId, stage.DeviceId);
        Assert.NotNull(stage.Command);
        Assert.Contains(db.ChangeTracker.Entries<SessionEntity>(), entry => entry.State == EntityState.Added);
        Assert.Contains(db.ChangeTracker.Entries<LedgerEntryEntity>(), entry => entry.State == EntityState.Added);
        Assert.Contains(db.ChangeTracker.Entries<DeviceCommandEntity>(), entry => entry.State == EntityState.Added);
        Assert.Contains(db.ChangeTracker.Entries<SessionCommandIdempotencyEntity>(), entry => entry.State == EntityState.Added);
        Assert.Empty(dispatcher.Notifications);
        Assert.Empty(lifecycle.Events);

        await transaction.RollbackAsync();
        db.ChangeTracker.Clear();

        Assert.Empty(await db.Sessions.ToListAsync());
        Assert.Empty(await db.LedgerEntries.ToListAsync());
        Assert.Empty(await db.DeviceCommands.ToListAsync());
        Assert.Empty(await db.SessionCommandIdempotency.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StageAsync_GuestAndLinkedPlayerComp_SkipOrdinaryBillingLedgerButStageSessionEffects(
        bool linkedPlayer)
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db);
        var dispatcher = new TrackingCommandDispatchService(db);
        var billing = new TrackingSessionBillingService(db);
        var lifecycle = new RecordingSessionLifecycleNotifier();
        var workflow = new EfSessionStartWorkflow(
            db,
            dispatcher,
            new FakeSessionLeaseSigner(),
            new FixedTimeProvider(Now),
            billing,
            lifecycle,
            new EfPlanLimitGuard(db));
        var request = new StartGuestSessionRequest(
            TestIds.OrganizationId,
            SeatId,
            TariffRuleVersionId: "comp-v1",
            IdempotencyKey: "linked-player-comp",
            DurationMode: SessionDurationModes.Fixed,
            DurationMinutes: 60,
            PlayerAccountId: linkedPlayer ? PlayerAccountId : null,
            BillingMode: string.Empty,
            TariffVersionId: Guid.NewGuid(),
            IsComp: true,
            CompReason: "approved loyalty compensation");

        var stage = await workflow.StageAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            request,
            actorCanApproveComp: true,
            SessionOriginNames.Operator,
            CancellationToken.None);

        Assert.True(stage.Result.Succeeded, stage.Result.Error);
        Assert.Equal(0, billing.AppendStartCalls);
        Assert.Empty(db.ChangeTracker.Entries<LedgerEntryEntity>());
        var session = Assert.Single(db.ChangeTracker.Entries<SessionEntity>()).Entity;
        Assert.True(session.IsComp);
        Assert.Equal(linkedPlayer ? PlayerAccountId : null, session.PlayerAccountId);
        Assert.Single(db.ChangeTracker.Entries<SessionLeaseEntity>());
        Assert.Single(db.ChangeTracker.Entries<SessionEventEntity>());
        Assert.Single(db.ChangeTracker.Entries<DeviceCommandEntity>());
        Assert.Single(db.ChangeTracker.Entries<SessionCommandIdempotencyEntity>());
    }

    [Theory]
    [InlineData(EntityState.Unchanged)]
    [InlineData(EntityState.Modified)]
    public async Task SessionBilling_TrackedPersistedState_DoesNotBypassDatabaseLookup(EntityState state)
    {
        await using var db = CreateDbContext();
        var sessionId = Guid.NewGuid();
        var session = new SessionEntity
        {
            SessionId = sessionId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            SeatId = SeatId,
            DeviceId = TestIds.DeviceId,
            CreatedByStaffUserId = ActorStaffUserId,
            State = SessionStateNames.Active,
            RequestedAtUtc = Now,
            StartedAtUtc = Now,
            UpdatedAtUtc = Now,
            Version = 1
        };
        db.Attach(session);
        db.Entry(session).State = state;
        var timeProvider = new FixedTimeProvider(Now);
        var billing = new SessionBillingService(
            db,
            new EfTariffService(db, timeProvider),
            new AlwaysOpenShiftResolver(),
            new LoyaltyAccrualService(db, AlwaysEnabledOrganizationEntitlements.Instance),
            timeProvider);
        var validation = new SessionBillingValidationResult(
            true,
            null,
            "manual-v1",
            null,
            3600,
            100,
            "TJS");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            billing.AppendStartLedgerEntriesAsync(
                sessionId,
                ActorStaffUserId,
                validation,
                PlayerAccountId,
                playerPackageId: null,
                BillingModeNames.PrepaidWallet,
                Now,
                CancellationToken.None));

        Assert.Contains("Sequence contains no elements", exception.Message, StringComparison.Ordinal);
        Assert.Empty(db.ChangeTracker.Entries<LedgerEntryEntity>());
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new PlatformDbContext(options);
    }

    /// <summary>
    /// Найдено ревью: гостевая сессия проходит мимо биллинга (режим оплаты пуст), но версию
    /// тарифа на себе уносит — из неё считают открытый счёт при закрытии. Значит и запрет
    /// расписания к ней относится: иначе утренний тариф прикрепляется к гостю в восемь вечера.
    /// </summary>
    [Fact]
    public async Task StageAsync_GuestSession_RefusesATariffOutsideItsHours()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db);
        var morningVersionId = await SeedMorningTariffAsync(db);
        var evening = DateTimeOffset.Parse("2026-05-13T20:00:00Z");

        var stage = await StageGuestAsync(db, morningVersionId, evening);

        Assert.False(stage.Result.Succeeded);
        Assert.Equal(TariffSchedule.OutsideHoursCode, stage.Result.Error);
    }

    [Fact]
    public async Task StageAsync_GuestSession_AcceptsTheSameTariffInsideItsHours()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db);
        var morningVersionId = await SeedMorningTariffAsync(db);

        var stage = await StageGuestAsync(db, morningVersionId, Now);

        Assert.True(stage.Result.Succeeded);
    }

    private static async Task<SessionStartStage> StageGuestAsync(
        PlatformDbContext db,
        Guid tariffVersionId,
        DateTimeOffset now)
    {
        var workflow = new EfSessionStartWorkflow(
            db,
            new TrackingCommandDispatchService(db),
            new FakeSessionLeaseSigner(),
            new FixedTimeProvider(now),
            new TrackingSessionBillingService(db),
            new RecordingSessionLifecycleNotifier(),
            new EfPlanLimitGuard(db));

        // Пустой режим оплаты — это и есть гость: биллинг такую сессию не считает.
        var request = new StartGuestSessionRequest(
            TestIds.OrganizationId,
            SeatId,
            TariffRuleVersionId: tariffVersionId.ToString("D"),
            IdempotencyKey: $"guest-{tariffVersionId:N}-{now.Ticks}",
            DurationMode: SessionDurationModes.Fixed,
            DurationMinutes: 60,
            PlayerAccountId: null,
            BillingMode: string.Empty);

        return await workflow.StageAsync(
            TestIds.BranchId, ActorStaffUserId, request, actorCanApproveComp: false, SessionOriginNames.Operator, CancellationToken.None);
    }

    /// <summary>Тариф, действующий с 08:00 до 16:00 каждый день.</summary>
    private static async Task<Guid> SeedMorningTariffAsync(PlatformDbContext db)
    {
        var tariffId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        db.Tariffs.Add(new TariffEntity
        {
            TariffId = tariffId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Утренний",
            IsActive = true,
            AppliesFromMinuteOfDay = 8 * 60,
            AppliesToMinuteOfDay = 16 * 60,
            CreatedAtUtc = Now
        });
        db.TariffVersions.Add(new TariffVersionEntity
        {
            TariffVersionId = versionId,
            TariffId = tariffId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            VersionNumber = 1,
            CurrencyCode = "TJS",
            PricePerMinuteMinorUnits = 10,
            MinimumBillableMinutes = 1,
            RoundingIncrementMinutes = 1,
            EffectiveFromUtc = Now.AddYears(-1)
        });
        await db.SaveChangesAsync();
        return versionId;
    }

    private static async Task SeedLayoutAsync(PlatformDbContext db)
    {
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = TestIds.OrganizationId,
            Name = "Demo Org",
            CreatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = TestIds.BranchId,
            OrganizationId = TestIds.OrganizationId,
            Name = "Demo Branch",
            CreatedAtUtc = Now
        });
        db.Zones.Add(new ZoneEntity
        {
            ZoneId = ZoneId,
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
            ZoneId = ZoneId,
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
            AgentVersion = "0.1.0",
            ShellVersion = "0.1.0",
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
        await db.SaveChangesAsync();
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
                Status = "Pending",
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

    private sealed class TrackingSessionBillingService(PlatformDbContext db) : ISessionBillingService
    {
        public int AppendStartCalls { get; private set; }

        public Task<SessionBillingValidationResult> ValidateStartAsync(
            Guid organizationId,
            Guid branchId,
            Guid? playerAccountId,
            string billingMode,
            Guid? tariffVersionId,
            Guid? playerPackageId,
            int durationMinutes,
            CancellationToken cancellationToken) => Task.FromResult(Valid(durationMinutes, billingMode));

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
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                PlayerAccountId = playerAccountId,
                SessionId = sessionId,
                EntryType = "session-charge",
                AccountType = "cash",
                AmountMinorUnits = -100,
                QuantitySeconds = validation.BillableSeconds,
                CurrencyCode = validation.CurrencyCode,
                Description = "Staged session charge",
                Reason = "session-start",
                CreatedByStaffUserId = actorStaffUserId,
                CreatedAtUtc = now
            });
            return Task.CompletedTask;
        }

        public Task<SessionBillingValidationResult> ComputeCompValueAsync(Guid organizationId, Guid branchId, Guid tariffVersionId, int durationMinutes, CancellationToken cancellationToken) => Task.FromResult(Valid(durationMinutes, BillingModeNames.PrepaidWallet));
        public Task<SessionBillingValidationResult> ValidateExtendAsync(Guid organizationId, Guid branchId, Guid? playerAccountId, string billingMode, Guid? tariffVersionId, Guid? playerPackageId, int additionalMinutes, CancellationToken cancellationToken) => Task.FromResult(Valid(additionalMinutes, billingMode));
        public Task AppendExtendLedgerEntriesAsync(Guid sessionId, Guid actorStaffUserId, SessionBillingValidationResult validation, Guid playerAccountId, Guid? playerPackageId, string billingMode, DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<SessionBillingValidationResult> ComputeCheckoutChargeAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult(Valid(0, BillingModeNames.PrepaidWallet));
        public Task AppendCheckoutLedgerEntriesAsync(Guid sessionId, Guid actorStaffUserId, SessionBillingValidationResult validation, Guid playerAccountId, DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;

        // Настоящий биллинг для пустого режима оплаты (гость) отдаёт ПУСТОЙ идентификатор версии
        // тарифа, и рабочий процесс подставляет вместо него то, что прислал клиент. Фейк, всегда
        // возвращавший «manual-v1», прятал этот путь целиком.
        private static SessionBillingValidationResult Valid(int minutes, string billingMode) => new(
            true,
            null,
            string.IsNullOrWhiteSpace(billingMode) ? string.Empty : "manual-v1",
            null,
            minutes * 60,
            100,
            "TJS");
    }

    private sealed class AlwaysOpenShiftResolver : IOpenShiftResolver
    {
        public Task<BillingCommandServiceResult<Guid>> GetOpenShiftIdAsync(
            Guid organizationId,
            Guid branchId,
            CancellationToken cancellationToken) =>
            Task.FromResult(BillingCommandServiceResult<Guid>.Ok(Guid.NewGuid()));
    }

    private sealed class FakeSessionLeaseSigner : ISessionLeaseSigner
    {
        public SessionLeaseDto Sign(Guid SessionId, Guid OrganizationId, Guid BranchId, Guid SeatId, Guid DeviceId, string State, int Sequence, DateTimeOffset IssuedAtUtc, DateTimeOffset ExpiresAtUtc) =>
            new(SessionId, OrganizationId, BranchId, SeatId, DeviceId, State, Sequence, IssuedAtUtc, ExpiresAtUtc, EcdsaSessionLeaseSigner.SignatureAlgorithm, $"fake-signature-{Sequence}");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
