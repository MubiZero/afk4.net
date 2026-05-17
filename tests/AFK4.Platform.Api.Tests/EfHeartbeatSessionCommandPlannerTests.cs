using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests;

public sealed class EfHeartbeatSessionCommandPlannerTests
{
    private static readonly Guid OrganizationId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid BranchId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid SeatId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
    private static readonly Guid DeviceId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
    private static readonly Guid SessionId = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee");
    private static readonly Guid StaffUserId = Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-16T10:00:00Z");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task PlanAsync_WithLocalSessionButNoCloudSession_ReturnsLockCommand()
    {
        await using var db = CreateDbContext();
        var planner = CreatePlanner(db);

        var plans = await planner.PlanAsync(
            DeviceId,
            CreateHeartbeat(activeSessionId: SessionId),
            CancellationToken.None);

        var plan = Assert.Single(plans);
        Assert.Equal(DeviceId, plan.DeviceId);
        Assert.Equal(DeviceCommandTypeNames.Lock, plan.Command.Type);
        Assert.Equal(SessionId.ToString("D"), plan.Command.Payload["sessionId"]);
        Assert.Equal("heartbeat-session-missing", plan.Command.Payload["reason"]);
        Assert.Empty(db.SessionLeases);
        Assert.Empty(db.SessionEvents);
    }

    [Fact]
    public async Task PlanAsync_WithActiveCloudSessionAndMissingLocalLease_IssuesUnlockWithFreshLease()
    {
        await using var db = CreateDbContext();
        await SeedSessionAsync(db, SessionStateNames.Active);
        var signer = new RecordingSessionLeaseSigner();
        var planner = CreatePlanner(db, signer);

        var plans = await planner.PlanAsync(
            DeviceId,
            CreateHeartbeat(activeSessionId: null),
            CancellationToken.None);

        var plan = Assert.Single(plans);
        Assert.Equal(DeviceCommandTypeNames.Unlock, plan.Command.Type);
        Assert.Equal(SessionId.ToString("D"), plan.Command.Payload["sessionId"]);
        Assert.Equal("heartbeat-session-continue", plan.Command.Payload["reason"]);

        var payloadLease = JsonSerializer.Deserialize<SessionLeaseDto>(
            plan.Command.Payload["sessionLease"],
            JsonOptions);
        Assert.NotNull(payloadLease);
        Assert.Equal(SessionId, payloadLease.SessionId);
        Assert.Equal(1, payloadLease.Sequence);
        Assert.Equal(Now.AddMinutes(15), payloadLease.ExpiresAtUtc);

        var lease = await db.SessionLeases.SingleAsync();
        var session = await db.Sessions.SingleAsync();
        Assert.Equal(lease.SessionLeaseId, session.CurrentLeaseId);
        Assert.Single(signer.SignedLeases);

        var sessionEvent = await db.SessionEvents.SingleAsync();
        Assert.Equal("session-lease-refreshed-by-heartbeat", sessionEvent.EventType);
        Assert.Equal(DeviceId, sessionEvent.DeviceId);
    }

    [Fact]
    public async Task PlanAsync_WithMatchingLeaseNearExpiry_IssuesRefreshLeaseCommand()
    {
        await using var db = CreateDbContext();
        await SeedSessionAsync(
            db,
            SessionStateNames.Active,
            currentLeaseSequence: 2,
            currentLeaseExpiresAtUtc: Now.AddMinutes(4));
        var planner = CreatePlanner(db);

        var plans = await planner.PlanAsync(
            DeviceId,
            CreateHeartbeat(
                activeSessionId: SessionId,
                activeSessionLeaseExpiresAtUtc: Now.AddMinutes(4),
                activeSessionLeaseSequence: 2),
            CancellationToken.None);

        var plan = Assert.Single(plans);
        Assert.Equal(DeviceCommandTypeNames.RefreshSessionLease, plan.Command.Type);
        Assert.Equal("heartbeat-lease-refresh", plan.Command.Payload["reason"]);

        var payloadLease = JsonSerializer.Deserialize<SessionLeaseDto>(
            plan.Command.Payload["sessionLease"],
            JsonOptions);
        Assert.NotNull(payloadLease);
        Assert.Equal(3, payloadLease.Sequence);

        Assert.Equal(2, await db.SessionLeases.CountAsync());
        var session = await db.Sessions.SingleAsync();
        var currentLease = await db.SessionLeases.SingleAsync(lease => lease.SessionLeaseId == session.CurrentLeaseId);
        Assert.Equal(3, currentLease.Sequence);
    }

    [Fact]
    public async Task PlanAsync_WithMatchingHealthyLease_ReturnsNoCommand()
    {
        await using var db = CreateDbContext();
        await SeedSessionAsync(
            db,
            SessionStateNames.Active,
            currentLeaseSequence: 2,
            currentLeaseExpiresAtUtc: Now.AddMinutes(10));
        var signer = new RecordingSessionLeaseSigner();
        var planner = CreatePlanner(db, signer);

        var plans = await planner.PlanAsync(
            DeviceId,
            CreateHeartbeat(
                activeSessionId: SessionId,
                activeSessionLeaseExpiresAtUtc: Now.AddMinutes(10),
                activeSessionLeaseSequence: 2),
            CancellationToken.None);

        Assert.Empty(plans);
        Assert.Empty(signer.SignedLeases);
        Assert.Equal(1, await db.SessionLeases.CountAsync());
        Assert.Empty(db.SessionEvents);
    }

    [Fact]
    public async Task PlanAsync_WithPendingSessionCommand_DoesNotCreateDuplicateCommandOrLease()
    {
        await using var db = CreateDbContext();
        await SeedSessionAsync(db, SessionStateNames.Active);
        db.DeviceCommands.Add(new DeviceCommandEntity
        {
            CommandId = Guid.Parse("99999999-9999-4999-8999-999999999999"),
            DeviceId = DeviceId,
            Type = DeviceCommandTypeNames.Unlock,
            PayloadJson = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["sessionId"] = SessionId.ToString("D"),
                ["reason"] = "heartbeat-session-continue"
            }, JsonOptions),
            Status = "Pending",
            Message = null,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        var signer = new RecordingSessionLeaseSigner();
        var planner = CreatePlanner(db, signer);

        var plans = await planner.PlanAsync(
            DeviceId,
            CreateHeartbeat(activeSessionId: null),
            CancellationToken.None);

        Assert.Empty(plans);
        Assert.Empty(signer.SignedLeases);
        Assert.Empty(db.SessionLeases);
        Assert.Empty(db.SessionEvents);
    }

    [Fact]
    public async Task PlanAsync_WithEndingCloudSession_ReturnsLockCommand()
    {
        await using var db = CreateDbContext();
        await SeedSessionAsync(db, SessionStateNames.Ending);
        var planner = CreatePlanner(db);

        var plans = await planner.PlanAsync(
            DeviceId,
            CreateHeartbeat(activeSessionId: SessionId),
            CancellationToken.None);

        var plan = Assert.Single(plans);
        Assert.Equal(DeviceCommandTypeNames.Lock, plan.Command.Type);
        Assert.Equal(SessionId.ToString("D"), plan.Command.Payload["sessionId"]);
        Assert.Equal("heartbeat-session-ending", plan.Command.Payload["reason"]);
        Assert.Empty(db.SessionLeases);
        Assert.Empty(db.SessionEvents);
    }

    [Fact]
    public async Task PlanAsync_WithEndingCloudSessionAndAcceptedLock_FinalizesSessionWithoutDuplicateLock()
    {
        await using var db = CreateDbContext();
        await SeedSessionAsync(
            db,
            SessionStateNames.Ending,
            currentLeaseSequence: 1,
            currentLeaseExpiresAtUtc: Now.AddMinutes(10));
        SeedDeviceCommand(
            db,
            DeviceCommandTypeNames.Lock,
            "Accepted",
            Now.AddMinutes(2),
            new Dictionary<string, string>
            {
                ["sessionId"] = SessionId.ToString("D"),
                ["reason"] = "heartbeat-session-ending"
            });
        await db.SaveChangesAsync();
        var planner = CreatePlanner(db);

        var plans = await planner.PlanAsync(
            DeviceId,
            CreateHeartbeat(activeSessionId: SessionId),
            CancellationToken.None);

        Assert.Empty(plans);

        var session = await db.Sessions.SingleAsync();
        Assert.Equal(SessionStateNames.Ended, session.State);
        Assert.Equal(Now.AddMinutes(2), session.EndedAtUtc);
        Assert.Null(session.CurrentLeaseId);
        Assert.Equal(Now.AddMinutes(2), session.UpdatedAtUtc);

        var sessionEvent = await db.SessionEvents.SingleAsync();
        Assert.Equal("session-ended", sessionEvent.EventType);
        Assert.Equal(DeviceId, sessionEvent.DeviceId);
        Assert.Contains("heartbeat-lock-result", sessionEvent.DetailsJson);
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PlatformDbContext(options);
    }

    private static EfHeartbeatSessionCommandPlanner CreatePlanner(
        PlatformDbContext dbContext,
        RecordingSessionLeaseSigner? signer = null)
    {
        return new EfHeartbeatSessionCommandPlanner(
            dbContext,
            signer ?? new RecordingSessionLeaseSigner(),
            Options.Create(new SessionLeaseOptions { LeaseMinutes = 15 }),
            new FixedTimeProvider(Now));
    }

    private static DeviceHeartbeatRequest CreateHeartbeat(
        Guid? activeSessionId,
        DateTimeOffset? activeSessionLeaseExpiresAtUtc = null,
        int? activeSessionLeaseSequence = null)
    {
        return new DeviceHeartbeatRequest(
            OrganizationId,
            BranchId,
            DeviceId,
            MachineName: "PC-001",
            AgentVersion: "0.1.0",
            ShellVersion: "0.1.0",
            ObservedAtUtc: Now,
            IsLocked: false,
            ActiveSessionId: activeSessionId,
            ActiveSessionLeaseExpiresAtUtc: activeSessionLeaseExpiresAtUtc,
            ActiveSessionLeaseSequence: activeSessionLeaseSequence);
    }

    private static async Task SeedSessionAsync(
        PlatformDbContext dbContext,
        string state,
        int? currentLeaseSequence = null,
        DateTimeOffset? currentLeaseExpiresAtUtc = null)
    {
        var session = new SessionEntity
        {
            SessionId = SessionId,
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            SeatId = SeatId,
            DeviceId = DeviceId,
            CreatedByStaffUserId = StaffUserId,
            PlayerKind = "guest",
            PlayerAccountId = null,
            TariffRuleVersionId = "manual-v1",
            State = state,
            RequestedAtUtc = Now,
            StartedAtUtc = Now,
            EndsAtUtc = Now.AddHours(1),
            EndedAtUtc = null,
            CurrentLeaseId = null,
            UpdatedAtUtc = Now
        };

        dbContext.Sessions.Add(session);

        if (currentLeaseSequence is not null)
        {
            var lease = CreateLease(session, currentLeaseSequence.Value, currentLeaseExpiresAtUtc ?? Now.AddMinutes(15));
            var leaseEntity = CreateLeaseEntity(lease);
            session.CurrentLeaseId = leaseEntity.SessionLeaseId;
            dbContext.SessionLeases.Add(leaseEntity);
        }

        await dbContext.SaveChangesAsync();
    }

    private static SessionLeaseDto CreateLease(
        SessionEntity session,
        int sequence,
        DateTimeOffset expiresAtUtc)
    {
        return new SessionLeaseDto(
            session.SessionId,
            session.OrganizationId,
            session.BranchId,
            session.SeatId,
            session.DeviceId,
            session.State,
            sequence,
            Now.AddMinutes(-1),
            expiresAtUtc,
            "test-signature",
            $"seed-signature-{sequence}");
    }

    private static SessionLeaseEntity CreateLeaseEntity(SessionLeaseDto lease)
    {
        return new SessionLeaseEntity
        {
            SessionLeaseId = Guid.NewGuid(),
            SessionId = lease.SessionId,
            OrganizationId = lease.OrganizationId,
            BranchId = lease.BranchId,
            SeatId = lease.SeatId,
            DeviceId = lease.DeviceId,
            State = lease.State,
            Sequence = lease.Sequence,
            IssuedAtUtc = lease.IssuedAtUtc,
            ExpiresAtUtc = lease.ExpiresAtUtc,
            SignatureAlgorithm = lease.SignatureAlgorithm,
            Signature = lease.Signature,
            PayloadJson = JsonSerializer.Serialize(lease, JsonOptions),
            RevokedAtUtc = null
        };
    }

    private static void SeedDeviceCommand(
        PlatformDbContext dbContext,
        string type,
        string status,
        DateTimeOffset updatedAtUtc,
        Dictionary<string, string> payload)
    {
        dbContext.DeviceCommands.Add(new DeviceCommandEntity
        {
            CommandId = Guid.NewGuid(),
            DeviceId = DeviceId,
            Type = type,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
            Status = status,
            Message = "seeded command result",
            CreatedAtUtc = updatedAtUtc.AddMinutes(-1),
            UpdatedAtUtc = updatedAtUtc
        });
    }

    private sealed class RecordingSessionLeaseSigner : ISessionLeaseSigner
    {
        public List<SessionLeaseDto> SignedLeases { get; } = [];

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
            var lease = new SessionLeaseDto(
                SessionId,
                OrganizationId,
                BranchId,
                SeatId,
                DeviceId,
                State,
                Sequence,
                IssuedAtUtc,
                ExpiresAtUtc,
                "test-signature",
                $"heartbeat-signature-{Sequence}");
            SignedLeases.Add(lease);

            return lease;
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
