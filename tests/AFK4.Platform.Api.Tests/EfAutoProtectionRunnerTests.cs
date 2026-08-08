using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Sessions;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

using AFK4.Platform.Api.Loyalty;

namespace AFK4.Platform.Api.Tests;

public sealed class EfAutoProtectionRunnerTests
{
    private static readonly Guid SeatId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ActorStaffUserId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid PlayerAccountId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid TariffVersionId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
    private static readonly Guid SessionId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-13T10:00:00Z");

    [Fact]
    public async Task RunOnceAsync_FixedSession_WarnsFiveMinutesBeforeExpiry()
    {
        await using var db = CreateDbContext();
        await SeedCoreAsync(db);
        await SeedSessionAsync(db, endsAtUtc: Now.AddMinutes(4), startedAtUtc: Now.AddMinutes(-56));
        var dispatcher = new RecordingDispatch();

        await CreateRunner(db, dispatcher).RunOnceAsync(CancellationToken.None);

        Assert.Equal(DeviceCommandTypeNames.Warn, Assert.Single(dispatcher.Commands).Type);
        var session = await db.Sessions.SingleAsync();
        Assert.Equal(Now, session.AutoWarnedAtUtc);
        Assert.Null(session.AutoLockedAtUtc);
    }

    [Fact]
    public async Task RunOnceAsync_FixedSession_LocksAtExpiry()
    {
        await using var db = CreateDbContext();
        await SeedCoreAsync(db);
        await SeedSessionAsync(db, endsAtUtc: Now.AddMinutes(-1), startedAtUtc: Now.AddMinutes(-61));
        var dispatcher = new RecordingDispatch();

        await CreateRunner(db, dispatcher).RunOnceAsync(CancellationToken.None);

        Assert.Equal(DeviceCommandTypeNames.Lock, Assert.Single(dispatcher.Commands).Type);
        var session = await db.Sessions.SingleAsync();
        Assert.Equal(Now, session.AutoLockedAtUtc);
        // The session stays active (time-up, awaiting checkout).
        Assert.Equal(SessionStateNames.Active, session.State);
    }

    [Fact]
    public async Task RunOnceAsync_FixedSession_DoesNotReWarn()
    {
        await using var db = CreateDbContext();
        await SeedCoreAsync(db);
        await SeedSessionAsync(db, endsAtUtc: Now.AddMinutes(4), startedAtUtc: Now.AddMinutes(-56));
        var dispatcher = new RecordingDispatch();
        var runner = CreateRunner(db, dispatcher);

        await runner.RunOnceAsync(CancellationToken.None);
        await runner.RunOnceAsync(CancellationToken.None);

        Assert.Single(dispatcher.Commands);
    }

    [Fact]
    public async Task RunOnceAsync_OpenTab_WarnsAndLocksAtBranchCreditLimit()
    {
        await using var db = CreateDbContext();
        await SeedCoreAsync(db, branchLimit: 2000);
        // Open tab started 40 min ago -> accrued 2250 (>= 2000).
        await SeedSessionAsync(db, endsAtUtc: null, startedAtUtc: Now.AddMinutes(-40));
        var dispatcher = new RecordingDispatch();

        await CreateRunner(db, dispatcher).RunOnceAsync(CancellationToken.None);

        Assert.Contains(dispatcher.Commands, command => command.Type == DeviceCommandTypeNames.Warn);
        Assert.Contains(dispatcher.Commands, command => command.Type == DeviceCommandTypeNames.Lock);
        var session = await db.Sessions.SingleAsync();
        Assert.Equal(Now, session.AutoLockedAtUtc);
        Assert.Equal(SessionStateNames.Active, session.State);
    }

    [Fact]
    public async Task RunOnceAsync_OpenTab_PlayerOverrideTakesPrecedenceOverBranchDefault()
    {
        await using var db = CreateDbContext();
        await SeedCoreAsync(db, branchLimit: 5000, playerLimit: 1000);
        await SeedSessionAsync(db, endsAtUtc: null, startedAtUtc: Now.AddMinutes(-40)); // accrued 2250 >= 1000.
        var dispatcher = new RecordingDispatch();

        await CreateRunner(db, dispatcher).RunOnceAsync(CancellationToken.None);

        Assert.Contains(dispatcher.Commands, command => command.Type == DeviceCommandTypeNames.Lock);
    }

    [Fact]
    public async Task RunOnceAsync_OpenTab_UnderLimit_TakesNoAction()
    {
        await using var db = CreateDbContext();
        await SeedCoreAsync(db, branchLimit: 5000);
        await SeedSessionAsync(db, endsAtUtc: null, startedAtUtc: Now.AddMinutes(-40)); // accrued 2250 < 5000.
        var dispatcher = new RecordingDispatch();

        await CreateRunner(db, dispatcher).RunOnceAsync(CancellationToken.None);

        Assert.Empty(dispatcher.Commands);
        var session = await db.Sessions.SingleAsync();
        Assert.Null(session.AutoLockedAtUtc);
    }

    [Fact]
    public async Task RunOnceAsync_OpenTab_NoLimit_IsUnbounded()
    {
        await using var db = CreateDbContext();
        await SeedCoreAsync(db);
        await SeedSessionAsync(db, endsAtUtc: null, startedAtUtc: Now.AddMinutes(-40));
        var dispatcher = new RecordingDispatch();

        await CreateRunner(db, dispatcher).RunOnceAsync(CancellationToken.None);

        Assert.Empty(dispatcher.Commands);
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new PlatformDbContext(options);
    }

    private static AutoProtectionRunner CreateRunner(PlatformDbContext db, RecordingDispatch dispatcher)
    {
        var timeProvider = new FixedTimeProvider(Now);
        return new AutoProtectionRunner(
            db,
            new SessionBillingService(db, new EfTariffService(db, timeProvider), new EfShiftService(db, timeProvider), new LoyaltyAccrualService(db, AlwaysEnabledOrganizationEntitlements.Instance), timeProvider),
            dispatcher,
            new AutoProtectionOptions(),
            timeProvider);
    }

    private static async Task SeedCoreAsync(PlatformDbContext db, long? branchLimit = null, long? playerLimit = null)
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
            PostpaidCreditLimitMinorUnits = branchLimit,
            CreatedAtUtc = Now
        });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerAccountId,
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = "Player One",
            IsActive = true,
            PostpaidCreditLimitMinorUnits = playerLimit,
            CreatedAtUtc = Now
        });
        var tariff = new TariffEntity
        {
            TariffId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Standard",
            IsActive = true,
            CreatedAtUtc = Now
        };
        db.Tariffs.Add(tariff);
        db.TariffVersions.Add(new TariffVersionEntity
        {
            TariffVersionId = TariffVersionId,
            TariffId = tariff.TariffId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            VersionNumber = 1,
            CurrencyCode = "TJS",
            PricePerMinuteMinorUnits = 50,
            MinimumBillableMinutes = 30,
            RoundingIncrementMinutes = 15,
            EffectiveFromUtc = Now.AddDays(-1),
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedSessionAsync(PlatformDbContext db, DateTimeOffset? endsAtUtc, DateTimeOffset startedAtUtc)
    {
        db.Sessions.Add(new SessionEntity
        {
            SessionId = SessionId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            SeatId = SeatId,
            DeviceId = TestIds.DeviceId,
            CreatedByStaffUserId = ActorStaffUserId,
            PlayerKind = "guest",
            PlayerAccountId = PlayerAccountId,
            TariffRuleVersionId = TariffVersionId.ToString("D"),
            State = SessionStateNames.Active,
            RequestedAtUtc = startedAtUtc,
            StartedAtUtc = startedAtUtc,
            EndsAtUtc = endsAtUtc,
            UpdatedAtUtc = startedAtUtc
        });
        await db.SaveChangesAsync();
    }

    private sealed class RecordingDispatch : IDeviceCommandDispatchService
    {
        public List<DeviceCommandDto> Commands { get; } = [];

        public Task<DeviceCommandDto> EnqueueAsync(Guid deviceId, CreateDeviceCommandRequest request, CancellationToken cancellationToken)
        {
            var command = new DeviceCommandDto(Guid.NewGuid(), request.Type, DateTimeOffset.UnixEpoch, request.Payload);
            Commands.Add(command);
            return Task.FromResult(command);
        }

        public Task NotifyAsync(Guid deviceId, DeviceCommandDto command, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<DeviceCommandDto> DispatchAsync(Guid deviceId, CreateDeviceCommandRequest request, CancellationToken cancellationToken) =>
            EnqueueAsync(deviceId, request, cancellationToken);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
