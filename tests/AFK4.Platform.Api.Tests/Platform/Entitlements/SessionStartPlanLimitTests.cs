using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.FloorMap;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Platform.Organizations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Platform.Entitlements;

public sealed class SessionStartPlanLimitTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid ZoneId = Guid.NewGuid();
    private static readonly Guid OccupiedSeatId = Guid.NewGuid();
    private static readonly Guid OccupiedDeviceId = Guid.NewGuid();
    private static readonly Guid FreeSeatId = Guid.NewGuid();
    private static readonly Guid FreeDeviceId = Guid.NewGuid();
    private static readonly Guid ActorStaffUserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Start_RefusesWithNumbers_WhenOrganizationIsAtSessionLimit()
    {
        await using var db = CreateDbContext();
        await SeedSceneAsync(db, maxConcurrentSessions: 1);
        var service = CreateService(db);
        var request = new StartGuestSessionRequest(
            OrganizationId,
            FreeSeatId,
            TariffRuleVersionId: "manual-v1",
            IdempotencyKey: "start-limit-refuse",
            DurationMode: SessionDurationModes.Fixed,
            DurationMinutes: 60);

        var result = await service.StartGuestSessionAsync(BranchId, ActorStaffUserId, request, SessionOriginNames.Operator, CancellationToken.None);

        Assert.True(result.Conflict);
        Assert.Equal(PlanLimitNames.ReachedCode, result.Code);
        Assert.NotNull(result.PlanLimit);
        Assert.Equal(1, result.PlanLimit!.Limit);
        Assert.Equal(1, result.PlanLimit.Current);
        Assert.Equal(1, await db.Sessions.CountAsync());
    }

    [Fact]
    public async Task Start_Succeeds_WhenBelowSessionLimit()
    {
        await using var db = CreateDbContext();
        await SeedSceneAsync(db, maxConcurrentSessions: 2);
        var service = CreateService(db);
        var request = new StartGuestSessionRequest(
            OrganizationId,
            FreeSeatId,
            TariffRuleVersionId: "manual-v1",
            IdempotencyKey: "start-limit-ok",
            DurationMode: SessionDurationModes.Fixed,
            DurationMinutes: 60);

        var result = await service.StartGuestSessionAsync(BranchId, ActorStaffUserId, request, SessionOriginNames.Operator, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.PlanLimit);
        Assert.Equal(2, await db.Sessions.CountAsync());
    }

    // Предел — один ПК на клуб: работает подключённый раньше (занятый), второй — вне тарифа.
    [Fact]
    public async Task Start_OnAPcOutsideTheFreePlan_IsRefused_AndTheMapMarksIt()
    {
        await using var db = CreateDbContext();
        await SeedSceneAsync(db, new OrganizationLimitsDto(null, null, null, null, MaxDevices: 1));
        var service = CreateService(db);

        var result = await service.StartGuestSessionAsync(BranchId, ActorStaffUserId, FreeSeatStart("start-outside-plan"),
            SessionOriginNames.Operator, CancellationToken.None);

        Assert.True(result.Conflict);
        Assert.Equal(PlanLimitNames.DeviceOutsidePlanCode, result.Code);
        Assert.Equal(PlanLimitNames.Devices, result.PlanLimit!.LimitName);
        Assert.Equal(1, result.PlanLimit.Limit);
        Assert.Equal(2, result.PlanLimit.Current);
        Assert.Equal(1, await db.Sessions.CountAsync());

        var map = (await new EfFloorMapReadService(db, new FixedTimeProvider(Now)).GetFloorMapAsync(BranchId, CancellationToken.None))!.FloorMap;
        Assert.True(map.Seats.Single(seat => seat.SeatId == FreeSeatId).IsOutsidePlan);
        Assert.False(map.Seats.Single(seat => seat.SeatId == OccupiedSeatId).IsOutsidePlan);
    }

    // Владелец оставил второй ПК: на нём играют, а занятый первый остаётся с идущей сессией.
    [Fact]
    public async Task Start_OnAPcTheOwnerKept_Runs()
    {
        await using var db = CreateDbContext();
        await SeedSceneAsync(db, new OrganizationLimitsDto(null, null, null, null, MaxDevices: 1));
        (await db.Devices.SingleAsync(device => device.DeviceId == FreeDeviceId)).KeptOnFreePlan = true;
        await db.SaveChangesAsync();

        var result = await CreateService(db).StartGuestSessionAsync(BranchId, ActorStaffUserId, FreeSeatStart("start-kept"),
            SessionOriginNames.Operator, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(2, await db.Sessions.CountAsync(session => session.State != SessionStateNames.Ended));
    }

    [Fact]
    public async Task Transfer_OntoAPcOutsideTheFreePlan_IsRefused()
    {
        await using var db = CreateDbContext();
        await SeedSceneAsync(db, new OrganizationLimitsDto(null, null, null, null, MaxDevices: 1));
        var running = await db.Sessions.SingleAsync();

        var result = await CreateService(db).TransferSessionAsync(running.SessionId, ActorStaffUserId,
            new TransferSessionRequest(FreeSeatId, "transfer-outside-plan"), CancellationToken.None);

        Assert.True(result.Conflict);
        Assert.Equal(PlanLimitNames.DeviceOutsidePlanCode, result.Code);
        Assert.Equal(OccupiedSeatId, (await db.Sessions.SingleAsync()).SeatId);
    }

    private static StartGuestSessionRequest FreeSeatStart(string idempotencyKey) => new(
        OrganizationId,
        FreeSeatId,
        TariffRuleVersionId: "manual-v1",
        IdempotencyKey: idempotencyKey,
        DurationMode: SessionDurationModes.Fixed,
        DurationMinutes: 60);

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PlatformDbContext(options);
    }

    private static EfSessionCommandService CreateService(PlatformDbContext db)
    {
        var dispatcher = new NoOpDeviceCommandDispatchService();
        var leaseSigner = new FakeSessionLeaseSigner();
        var timeProvider = new FixedTimeProvider(Now);
        var billing = new FakeSessionBillingService();
        var notifier = new RecordingSessionLifecycleNotifier();
        var planLimitGuard = new EfPlanLimitGuard(db);
        return new EfSessionCommandService(
            db,
            dispatcher,
            leaseSigner,
            timeProvider,
            billing,
            notifier,
            new EfSessionStartWorkflow(db, dispatcher, leaseSigner, timeProvider, billing, notifier, planLimitGuard),
            planLimitGuard);
    }

    // Место + одобренное устройство + тариф: одно место уже занято активным сеансом (упирается в
    // лимит), второе свободно (куда пробует стартовать тест).
    private static Task SeedSceneAsync(PlatformDbContext db, int maxConcurrentSessions) =>
        SeedSceneAsync(db, new OrganizationLimitsDto(null, null, maxConcurrentSessions, null));

    private static async Task SeedSceneAsync(PlatformDbContext db, OrganizationLimitsDto limits)
    {
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = OrganizationId,
            Slug = "club-" + OrganizationId.ToString("N")[..8],
            Name = "Клуб",
            Status = OrganizationStatusNames.Active,
            PlanCode = "growth",
            LimitsJson = OrganizationLimitsJson.Serialize(limits),
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = BranchId,
            OrganizationId = OrganizationId,
            Slug = "branch-" + BranchId.ToString("N")[..8],
            Name = "Филиал",
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
            SeatId = OccupiedSeatId,
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            ZoneId = ZoneId,
            Name = "PC-001",
            SortOrder = 1,
            CreatedAtUtc = Now
        });
        db.Devices.Add(new DeviceEntity
        {
            DeviceId = OccupiedDeviceId,
            OrganizationId = OrganizationId,
            BranchId = BranchId,
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
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            SeatId = OccupiedSeatId,
            DeviceId = OccupiedDeviceId,
            AttachedAtUtc = Now
        });
        db.Seats.Add(new SeatEntity
        {
            SeatId = FreeSeatId,
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            ZoneId = ZoneId,
            Name = "PC-002",
            SortOrder = 2,
            CreatedAtUtc = Now
        });
        db.Devices.Add(new DeviceEntity
        {
            DeviceId = FreeDeviceId,
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            MachineName = "PC-002",
            AgentVersion = "0.1.0",
            ShellVersion = "0.1.0",
            EnrolledAtUtc = Now.AddMinutes(1),
            IsOnline = true,
            IsLocked = true
        });
        db.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
        {
            DeviceSeatAssignmentId = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            SeatId = FreeSeatId,
            DeviceId = FreeDeviceId,
            AttachedAtUtc = Now
        });
        db.Sessions.Add(new SessionEntity
        {
            SessionId = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            SeatId = OccupiedSeatId,
            DeviceId = OccupiedDeviceId,
            CreatedByStaffUserId = ActorStaffUserId,
            PlayerKind = "guest",
            BillingMode = BillingModeNames.PostpaidDebt,
            State = SessionStateNames.Active,
            RequestedAtUtc = Now,
            StartedAtUtc = Now,
            UpdatedAtUtc = Now,
            Version = 1
        });

        await db.SaveChangesAsync();
    }

    private sealed class NoOpDeviceCommandDispatchService : IDeviceCommandDispatchService
    {
        public Task<DeviceCommandDto> EnqueueAsync(
            Guid deviceId,
            CreateDeviceCommandRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new DeviceCommandDto(Guid.NewGuid(), request.Type, DateTimeOffset.UtcNow, request.Payload));

        public Task NotifyAsync(Guid deviceId, DeviceCommandDto command, CancellationToken cancellationToken) =>
            Task.CompletedTask;

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
                $"test-signature-{Sequence}");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
