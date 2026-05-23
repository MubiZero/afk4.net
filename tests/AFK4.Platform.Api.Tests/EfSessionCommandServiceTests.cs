using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class EfSessionCommandServiceTests
{
    private static readonly Guid SeatId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid TargetSeatId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid TargetDeviceId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid ZoneId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ActorStaffUserId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-13T10:00:00Z");

    [Fact]
    public async Task StartGuestSessionAsync_CreatesActiveSessionLeaseEventIdempotencyAndUnlockCommand()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db, includeTargetSeat: false);
        var dispatcher = new RecordingCommandDispatchService();
        var service = CreateService(db, dispatcher);
        var request = new StartGuestSessionRequest(
            TestIds.OrganizationId,
            SeatId,
            DurationMinutes: 60,
            TariffRuleVersionId: "manual-v1",
            IdempotencyKey: "start-seat-1-20260513-1000");

        var result = await service.StartGuestSessionAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal(SessionStateNames.Active, result.Response.Session.State);
        Assert.Equal("manual-v1", result.Response.Session.TariffRuleVersionId);
        Assert.NotNull(result.Response.Session.CurrentLease);

        var session = await db.Sessions.SingleAsync();
        Assert.Equal(result.Response.Session.SessionId, session.SessionId);
        Assert.Equal(SessionStateNames.Active, session.State);
        Assert.Equal(SeatId, session.SeatId);
        Assert.Equal(TestIds.DeviceId, session.DeviceId);
        Assert.Equal(ActorStaffUserId, session.CreatedByStaffUserId);

        var lease = await db.SessionLeases.SingleAsync();
        Assert.Equal(session.SessionId, lease.SessionId);
        Assert.Equal(1, lease.Sequence);
        Assert.Equal(session.CurrentLeaseId, lease.SessionLeaseId);

        var sessionEvent = await db.SessionEvents.SingleAsync();
        Assert.Equal(session.SessionId, sessionEvent.SessionId);
        Assert.Equal("session-started", sessionEvent.EventType);

        var idempotency = await db.SessionCommandIdempotency.SingleAsync();
        Assert.Equal("start", idempotency.Operation);
        Assert.False(string.IsNullOrWhiteSpace(idempotency.ResponseJson));

        var call = Assert.Single(dispatcher.Calls);
        Assert.Equal(TestIds.DeviceId, call.DeviceId);
        Assert.Equal("unlock", call.Request.Type);
        Assert.Equal(session.SessionId.ToString("D"), call.Request.Payload["sessionId"]);
        Assert.Equal("session-start", call.Request.Payload["reason"]);
        Assert.False(string.IsNullOrWhiteSpace(call.Request.Payload["sessionLease"]));
    }

    [Fact]
    public async Task StartGuestSessionAsync_RepeatedWithSameIdempotencyKeyAndRequestReturnsOriginalResponse()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db, includeTargetSeat: false);
        var dispatcher = new RecordingCommandDispatchService();
        var service = CreateService(db, dispatcher);
        var request = new StartGuestSessionRequest(
            TestIds.OrganizationId,
            SeatId,
            DurationMinutes: 60,
            TariffRuleVersionId: "manual-v1",
            IdempotencyKey: "start-seat-1-20260513-1000");

        var first = await service.StartGuestSessionAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);
        var second = await service.StartGuestSessionAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(second.Succeeded);
        Assert.NotNull(first.Response);
        Assert.NotNull(second.Response);
        Assert.Equal(first.Response.Session.SessionId, second.Response.Session.SessionId);
        Assert.Equal(first.Response.DeviceCommands[0].CommandId, second.Response.DeviceCommands[0].CommandId);
        Assert.Single(dispatcher.Calls);
    }

    [Fact]
    public async Task StartGuestSessionAsync_ReusingIdempotencyKeyWithDifferentRequestReturnsConflict()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db, includeTargetSeat: true);
        var dispatcher = new RecordingCommandDispatchService();
        var service = CreateService(db, dispatcher);
        var firstRequest = new StartGuestSessionRequest(
            TestIds.OrganizationId,
            SeatId,
            DurationMinutes: 60,
            TariffRuleVersionId: "manual-v1",
            IdempotencyKey: "start-seat-1-20260513-1000");
        var differentRequest = firstRequest with { SeatId = TargetSeatId };

        await service.StartGuestSessionAsync(TestIds.BranchId, ActorStaffUserId, firstRequest, CancellationToken.None);
        var conflict = await service.StartGuestSessionAsync(TestIds.BranchId, ActorStaffUserId, differentRequest, CancellationToken.None);

        Assert.True(conflict.Conflict);
        Assert.Null(conflict.Response);
        Assert.Single(dispatcher.Calls);
        Assert.Single(dispatcher.Enqueued);
        Assert.Empty(dispatcher.DispatchCalls);
    }

    [Fact]
    public async Task EndSessionAsync_MovesActiveSessionToEndingAndDispatchesLockCommand()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db, includeTargetSeat: false);
        var dispatcher = new RecordingCommandDispatchService();
        var service = CreateService(db, dispatcher);
        var start = await service.StartGuestSessionAsync(
            TestIds.BranchId,
            ActorStaffUserId,
        new StartGuestSessionRequest(TestIds.OrganizationId, SeatId, 60, "manual-v1", "start-seat-1"),
            CancellationToken.None);
        Assert.NotNull(start.Response);
        dispatcher.Clear();

        var result = await service.EndSessionAsync(
            start.Response.Session.SessionId,
            ActorStaffUserId,
            new EndSessionRequest("operator-end", "end-session-1"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal(SessionStateNames.Ending, result.Response.Session.State);

        var session = await db.Sessions.SingleAsync();
        Assert.Equal(SessionStateNames.Ending, session.State);

        var call = Assert.Single(dispatcher.Calls);
        var enqueued = Assert.Single(dispatcher.Enqueued);
        Assert.Empty(dispatcher.DispatchCalls);
        Assert.Equal(call.DeviceId, enqueued.DeviceId);
        Assert.Equal(call.Request.Type, enqueued.Request.Type);
        Assert.Equal(TestIds.DeviceId, call.DeviceId);
        Assert.Equal("lock", call.Request.Type);
        Assert.Equal(session.SessionId.ToString("D"), call.Request.Payload["sessionId"]);
        Assert.Equal("operator-end", call.Request.Payload["reason"]);
    }

    [Fact]
    public async Task EndSessionAsync_WhenSessionAlreadyEnding_ReturnsCurrentEndingStateWithoutDispatchingAgain()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db, includeTargetSeat: false);
        var dispatcher = new RecordingCommandDispatchService();
        var service = CreateService(db, dispatcher);
        var start = await service.StartGuestSessionAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new StartGuestSessionRequest(TestIds.OrganizationId, SeatId, 60, "manual-v1", "start-seat-1"),
            CancellationToken.None);
        Assert.NotNull(start.Response);

        var firstEnd = await service.EndSessionAsync(
            start.Response.Session.SessionId,
            ActorStaffUserId,
            new EndSessionRequest("operator-end", "end-session-1"),
            CancellationToken.None);
        Assert.True(firstEnd.Succeeded);
        dispatcher.Clear();

        var secondEnd = await service.EndSessionAsync(
            start.Response.Session.SessionId,
            ActorStaffUserId,
            new EndSessionRequest("operator-end", "end-session-2"),
            CancellationToken.None);

        Assert.True(secondEnd.Succeeded);
        Assert.NotNull(secondEnd.Response);
        Assert.Equal(SessionStateNames.Ending, secondEnd.Response.Session.State);
        Assert.Empty(secondEnd.Response.DeviceCommands);
        Assert.Empty(dispatcher.Calls);
        Assert.Empty(dispatcher.Enqueued);
        Assert.Empty(dispatcher.DispatchCalls);
    }

    [Fact]
    public async Task TransferSessionAsync_DispatchesLockToOldDeviceAndUnlockToNewDevice()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db, includeTargetSeat: true);
        var dispatcher = new RecordingCommandDispatchService();
        var service = CreateService(db, dispatcher);
        var start = await service.StartGuestSessionAsync(
            TestIds.BranchId,
            ActorStaffUserId,
        new StartGuestSessionRequest(TestIds.OrganizationId, SeatId, 60, "manual-v1", "start-seat-1"),
            CancellationToken.None);
        Assert.NotNull(start.Response);
        dispatcher.Clear();

        var result = await service.TransferSessionAsync(
            start.Response.Session.SessionId,
            ActorStaffUserId,
            new TransferSessionRequest(TargetSeatId, "transfer-session-1"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal(TargetSeatId, result.Response.Session.SeatId);
        Assert.Equal(TargetDeviceId, result.Response.Session.DeviceId);
        Assert.Equal(SessionStateNames.Active, result.Response.Session.State);

        var session = await db.Sessions.SingleAsync();
        Assert.Equal(TargetSeatId, session.SeatId);
        Assert.Equal(TargetDeviceId, session.DeviceId);

        Assert.Equal(2, dispatcher.Calls.Count);
        Assert.Equal(2, dispatcher.Enqueued.Count);
        Assert.Empty(dispatcher.DispatchCalls);
        Assert.Equal(TestIds.DeviceId, dispatcher.Calls[0].DeviceId);
        Assert.Equal("lock", dispatcher.Calls[0].Request.Type);
        Assert.Equal(TargetDeviceId, dispatcher.Calls[1].DeviceId);
        Assert.Equal("unlock", dispatcher.Calls[1].Request.Type);
        Assert.Equal(session.SessionId.ToString("D"), dispatcher.Calls[1].Request.Payload["sessionId"]);
        Assert.False(string.IsNullOrWhiteSpace(dispatcher.Calls[1].Request.Payload["sessionLease"]));
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PlatformDbContext(options);
    }

    private static EfSessionCommandService CreateService(
        PlatformDbContext db,
        RecordingCommandDispatchService dispatcher)
    {
        return new EfSessionCommandService(
            db,
            dispatcher,
            new FakeSessionLeaseSigner(),
            new FixedTimeProvider(Now),
            new FakeSessionBillingService());
    }

    private static async Task SeedLayoutAsync(PlatformDbContext db, bool includeTargetSeat)
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

        if (includeTargetSeat)
        {
            db.Seats.Add(new SeatEntity
            {
                SeatId = TargetSeatId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                ZoneId = ZoneId,
                Name = "PC-002",
                SortOrder = 2,
                CreatedAtUtc = Now
            });
            db.Devices.Add(new DeviceEntity
            {
                DeviceId = TargetDeviceId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                MachineName = "PC-002",
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
                SeatId = TargetSeatId,
                DeviceId = TargetDeviceId,
                AttachedAtUtc = Now
            });
        }

        await db.SaveChangesAsync();
    }

    private sealed class RecordingCommandDispatchService : IDeviceCommandDispatchService
    {
        public List<(Guid DeviceId, CreateDeviceCommandRequest Request)> DispatchCalls { get; } = [];

        public List<(Guid DeviceId, CreateDeviceCommandRequest Request)> Enqueued { get; } = [];

        public List<(Guid DeviceId, CreateDeviceCommandRequest Request)> Calls { get; } = [];

        public Task<DeviceCommandDto> EnqueueAsync(
            Guid deviceId,
            CreateDeviceCommandRequest request,
            CancellationToken cancellationToken)
        {
            Enqueued.Add((deviceId, request));

            return Task.FromResult(new DeviceCommandDto(
                CommandId: Guid.NewGuid(),
                Type: request.Type,
                CreatedAtUtc: Now,
                Payload: request.Payload));
        }

        public Task NotifyAsync(
            Guid deviceId,
            DeviceCommandDto command,
            CancellationToken cancellationToken)
        {
            Calls.Add((deviceId, new CreateDeviceCommandRequest(command.Type, command.Payload)));

            return Task.CompletedTask;
        }

        public void Clear()
        {
            DispatchCalls.Clear();
            Enqueued.Clear();
            Calls.Clear();
        }

        public Task<DeviceCommandDto> DispatchAsync(
            Guid deviceId,
            CreateDeviceCommandRequest request,
            CancellationToken cancellationToken)
        {
            DispatchCalls.Add((deviceId, request));
            Calls.Add((deviceId, request));

            return Task.FromResult(new DeviceCommandDto(
                CommandId: Guid.NewGuid(),
                Type: request.Type,
                CreatedAtUtc: Now,
                Payload: request.Payload));
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
            DateTimeOffset ExpiresAtUtc)
        {
            return new SessionLeaseDto(
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
                $"fake-signature-{Sequence}");
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
