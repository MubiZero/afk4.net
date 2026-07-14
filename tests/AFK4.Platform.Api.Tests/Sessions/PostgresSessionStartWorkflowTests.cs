using System.Text.Json;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Sessions;

public sealed class PostgresSessionStartWorkflowTests
{
    [PostgresSessionFact]
    public async Task StartFailureAfterDependencySave_RollsBackEveryEffectAndNotifiesNobody()
    {
        await using var database = await SessionStartPostgresFixture.CreateAsync(
            Environment.GetEnvironmentVariable(PostgresSessionFactAttribute.EnvironmentVariable)!);
        await database.SeedAsync();
        await using var db = database.CreateDbContext();
        var dispatcher = new SavingCommandDispatchService(db);
        var billing = new TrackingSessionBillingService(db, database.OrganizationId, database.BranchId);
        var lifecycle = new RecordingLifecycleNotifier();
        var timeProvider = new FixedTimeProvider(database.Now);
        var innerWorkflow = new EfSessionStartWorkflow(
            db,
            dispatcher,
            new FakeSessionLeaseSigner(),
            timeProvider,
            billing,
            lifecycle);
        var failingWorkflow = new FailAfterStageWorkflow(innerWorkflow, db);
        var service = new EfSessionCommandService(
            db,
            dispatcher,
            new FakeSessionLeaseSigner(),
            timeProvider,
            billing,
            lifecycle,
            failingWorkflow);
        var request = new StartGuestSessionRequest(
            database.OrganizationId,
            database.SeatId,
            TariffRuleVersionId: "postgres-rollback",
            IdempotencyKey: "postgres-start-rollback",
            DurationMode: SessionDurationModes.Fixed,
            DurationMinutes: 60,
            PlayerAccountId: database.PlayerAccountId,
            BillingMode: BillingModeNames.PrepaidWallet);

        var exception = await Assert.ThrowsAsync<ForcedStageFailureException>(() =>
            service.StartGuestSessionAsync(
                database.BranchId,
                database.StaffUserId,
                request,
                CancellationToken.None));

        Assert.Equal("forced failure after dependency save", exception.Message);
        Assert.True(failingWorkflow.DependencySaveObserved);
        Assert.Empty(dispatcher.Notifications);
        Assert.Empty(lifecycle.Events);

        await using var verification = database.CreateDbContext();
        Assert.Empty(await verification.Sessions.AsNoTracking().ToListAsync());
        Assert.Empty(await verification.SessionLeases.AsNoTracking().ToListAsync());
        Assert.Empty(await verification.SessionEvents.AsNoTracking().ToListAsync());
        Assert.Empty(await verification.LedgerEntries.AsNoTracking().ToListAsync());
        Assert.Empty(await verification.DeviceCommands.AsNoTracking().ToListAsync());
        Assert.Empty(await verification.SessionCommandIdempotency.AsNoTracking().ToListAsync());
    }

    private sealed class FailAfterStageWorkflow(
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
            DependencySaveObserved = stage.Result.Succeeded &&
                await db.Sessions.AsNoTracking().AnyAsync(cancellationToken) &&
                await db.SessionLeases.AsNoTracking().AnyAsync(cancellationToken) &&
                await db.SessionEvents.AsNoTracking().AnyAsync(cancellationToken) &&
                await db.LedgerEntries.AsNoTracking().AnyAsync(cancellationToken) &&
                await db.DeviceCommands.AsNoTracking().AnyAsync(cancellationToken);
            throw new ForcedStageFailureException();
        }

        public Task NotifyCommittedAsync(SessionStartStage stage, CancellationToken cancellationToken) =>
            inner.NotifyCommittedAsync(stage, cancellationToken);
    }

    private sealed class ForcedStageFailureException()
        : InvalidOperationException("forced failure after dependency save");

    private sealed class SavingCommandDispatchService(PlatformDbContext db) : IDeviceCommandDispatchService
    {
        private readonly EfDeviceCommandStore store = new(db);

        public List<(Guid DeviceId, DeviceCommandDto Command)> Notifications { get; } = [];

        public async Task<DeviceCommandDto> EnqueueAsync(
            Guid deviceId,
            CreateDeviceCommandRequest request,
            CancellationToken cancellationToken)
        {
            var command = new DeviceCommandDto(Guid.NewGuid(), request.Type, DateTimeOffset.UtcNow, request.Payload);
            await store.AddPendingAsync(deviceId, command, cancellationToken);
            return command;
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

    private sealed class TrackingSessionBillingService(
        PlatformDbContext db,
        Guid organizationId,
        Guid branchId) : ISessionBillingService
    {
        public Task<SessionBillingValidationResult> ValidateStartAsync(Guid organizationId, Guid branchId, Guid? playerAccountId, string billingMode, Guid? tariffVersionId, Guid? playerPackageId, int durationMinutes, CancellationToken cancellationToken) => Task.FromResult(Valid(durationMinutes));
        public Task<SessionBillingValidationResult> ComputeCompValueAsync(Guid organizationId, Guid branchId, Guid tariffVersionId, int durationMinutes, CancellationToken cancellationToken) => Task.FromResult(Valid(durationMinutes));
        public Task<SessionBillingValidationResult> ValidateExtendAsync(Guid organizationId, Guid branchId, Guid? playerAccountId, string billingMode, Guid? tariffVersionId, Guid? playerPackageId, int additionalMinutes, CancellationToken cancellationToken) => Task.FromResult(Valid(additionalMinutes));
        public Task AppendExtendLedgerEntriesAsync(Guid sessionId, Guid actorStaffUserId, SessionBillingValidationResult validation, Guid playerAccountId, Guid? playerPackageId, string billingMode, DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<SessionBillingValidationResult> ComputeCheckoutChargeAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult(Valid(0));
        public Task AppendCheckoutLedgerEntriesAsync(Guid sessionId, Guid actorStaffUserId, SessionBillingValidationResult validation, Guid playerAccountId, DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;

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
                CurrencyCode = "TJS",
                Description = "rollback proof",
                Reason = "session-start",
                CreatedByStaffUserId = actorStaffUserId,
                CreatedAtUtc = now
            });
            return Task.CompletedTask;
        }

        private static SessionBillingValidationResult Valid(int minutes) =>
            new(true, null, "postgres-rollback", null, minutes * 60, 100, "TJS");
    }

    private sealed class RecordingLifecycleNotifier : ISessionLifecycleNotifier
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
        public SessionLeaseDto Sign(Guid SessionId, Guid OrganizationId, Guid BranchId, Guid SeatId, Guid DeviceId, string State, int Sequence, DateTimeOffset IssuedAtUtc, DateTimeOffset ExpiresAtUtc) =>
            new(SessionId, OrganizationId, BranchId, SeatId, DeviceId, State, Sequence, IssuedAtUtc, ExpiresAtUtc, EcdsaSessionLeaseSigner.SignatureAlgorithm, $"postgres-signature-{Sequence}");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

public sealed class PostgresSessionFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "AFK4_RESERVATION_POSTGRES_TEST_CONNECTION_STRING";

    public PostgresSessionFactAttribute()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString) || !IsTestDatabase(connectionString))
        {
            Skip = $"Set {EnvironmentVariable} to a PostgreSQL database whose name ends with _test.";
        }
    }

    private static bool IsTestDatabase(string connectionString)
    {
        try
        {
            return new NpgsqlConnectionStringBuilder(connectionString).Database?
                .EndsWith("_test", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

internal sealed class SessionStartPostgresFixture : IAsyncDisposable
{
    private readonly string connectionString;
    private readonly string schemaName;

    private SessionStartPostgresFixture(string connectionString, string schemaName)
    {
        this.connectionString = connectionString;
        this.schemaName = schemaName;
    }

    public Guid OrganizationId { get; } = Guid.Parse("71111111-1111-4111-8111-111111111111");
    public Guid BranchId { get; } = Guid.Parse("72222222-2222-4222-8222-222222222222");
    public Guid ZoneId { get; } = Guid.Parse("73333333-3333-4333-8333-333333333333");
    public Guid SeatId { get; } = Guid.Parse("74444444-4444-4444-8444-444444444444");
    public Guid DeviceId { get; } = Guid.Parse("75555555-5555-4555-8555-555555555555");
    public Guid PlayerAccountId { get; } = Guid.Parse("76666666-6666-4666-8666-666666666666");
    public Guid StaffUserId { get; } = Guid.Parse("77777777-7777-4777-8777-777777777777");
    public DateTimeOffset Now { get; } = DateTimeOffset.Parse("2026-07-14T14:00:00Z");

    public static async Task<SessionStartPostgresFixture> CreateAsync(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (builder.Database?.EndsWith("_test", StringComparison.OrdinalIgnoreCase) != true)
        {
            throw new InvalidOperationException("Session PostgreSQL tests require a database name ending in _test.");
        }

        var schemaName = $"session_start_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE SCHEMA \"{schemaName}\"";
            await command.ExecuteNonQueryAsync();
        }

        builder.SearchPath = schemaName;
        var fixture = new SessionStartPostgresFixture(builder.ConnectionString, schemaName);
        try
        {
            await using var db = fixture.CreateDbContext();
            await db.Database.MigrateAsync();
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    public PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new PlatformDbContext(options);
    }

    public async Task SeedAsync()
    {
        await using var db = CreateDbContext();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = OrganizationId,
            Slug = "postgres-session-start",
            Name = "PostgreSQL Session Start",
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = BranchId,
            OrganizationId = OrganizationId,
            Slug = "central",
            Name = "Central",
            CreatedAtUtc = Now
        });
        db.Zones.Add(new ZoneEntity
        {
            ZoneId = ZoneId,
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            Name = "Main Hall",
            SortOrder = 1,
            CreatedAtUtc = Now
        });
        db.Seats.Add(new SeatEntity
        {
            SeatId = SeatId,
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            ZoneId = ZoneId,
            Name = "PC-001",
            SortOrder = 1,
            CreatedAtUtc = Now
        });
        db.Devices.Add(new DeviceEntity
        {
            DeviceId = DeviceId,
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            MachineName = "PC-001",
            DisplayName = "PC-001",
            EnrolledAtUtc = Now,
            IsOnline = true,
            IsLocked = true
        });
        db.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
        {
            DeviceSeatAssignmentId = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            SeatId = SeatId,
            DeviceId = DeviceId,
            AttachedAtUtc = Now
        });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerAccountId,
            OrganizationId = OrganizationId,
            HomeBranchId = BranchId,
            DisplayName = "Rollback Player",
            IsActive = true,
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = null };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE";
        await command.ExecuteNonQueryAsync();
    }
}
