using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Приёмка 30.09.2026: игрок позвал администратора в 11:10, встал в 11:32, а в 13:52 свободный ПК всё
/// ещё «звал 2 ч 42 м» — и потом тот же «вызов» достался новому гостю. Вызов принадлежит человеку за
/// ПК: кончилась его сессия, кончился и вызов. Сессия кончается двумя путями — ответ ПК на lock
/// (процессор результатов) и сердцебиение, нашедшее принятый lock.
/// </summary>
public sealed class SessionEndClearsAssistanceCallTests
{
    private static readonly Guid OrganizationId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid BranchId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid DeviceId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
    private static readonly Guid SessionId = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-30T11:32:00Z");
    private static readonly DateTimeOffset CalledAt = DateTimeOffset.Parse("2026-09-30T11:10:00Z");

    [Fact]
    public async Task LockResult_ThatEndsTheSession_PutsOutTheCall()
    {
        await using var db = CreateDbContext();
        var commandId = await SeedAsync(db, SessionStateNames.Ending, lockStatus: "Pending");
        var processor = new EfSessionCommandResultProcessor(db, new FixedTimeProvider(Now));

        await processor.ProcessAsync(
            new DeviceCommandResultDto(OrganizationId, BranchId, DeviceId, commandId, "Completed", "locked", Now),
            CancellationToken.None);

        Assert.Equal(SessionStateNames.Ended, (await db.Sessions.SingleAsync()).State);
        Assert.Null((await db.Devices.SingleAsync()).AssistanceRequestedAtUtc);
    }

    [Fact]
    public async Task LockResult_ForASessionThatIsNotEnding_LeavesTheCallAlone()
    {
        // Пауза тоже запирает ПК, но игрок вернётся — зовущий ждёт ответа стойки.
        await using var db = CreateDbContext();
        var commandId = await SeedAsync(db, SessionStateNames.Paused, lockStatus: "Pending");
        var processor = new EfSessionCommandResultProcessor(db, new FixedTimeProvider(Now));

        await processor.ProcessAsync(
            new DeviceCommandResultDto(OrganizationId, BranchId, DeviceId, commandId, "Completed", "locked", Now),
            CancellationToken.None);

        Assert.Equal(CalledAt, (await db.Devices.SingleAsync()).AssistanceRequestedAtUtc);
    }

    [Fact]
    public async Task Heartbeat_ThatFindsAnAcceptedLockForAnEndingSession_PutsOutTheCall()
    {
        await using var db = CreateDbContext();
        await SeedAsync(db, SessionStateNames.Ending, lockStatus: "Accepted");
        var planner = new EfHeartbeatSessionCommandPlanner(
            db,
            new ThrowingLeaseSigner(),
            Microsoft.Extensions.Options.Options.Create(new SessionLeaseOptions { LeaseMinutes = 15 }),
            new FixedTimeProvider(Now));

        var plans = await planner.PlanAsync(
            DeviceId,
            new DeviceHeartbeatRequest(
                OrganizationId, BranchId, DeviceId, "PC-012", "0.9.2", "0.9.2", Now,
                IsLocked: false, ActiveSessionId: SessionId,
                ActiveSessionLeaseExpiresAtUtc: null, ActiveSessionLeaseSequence: null),
            CancellationToken.None);

        Assert.Empty(plans);
        Assert.Equal(SessionStateNames.Ended, (await db.Sessions.SingleAsync()).State);
        Assert.Null((await db.Devices.SingleAsync()).AssistanceRequestedAtUtc);
    }

    private static PlatformDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static async Task<Guid> SeedAsync(PlatformDbContext db, string sessionState, string lockStatus)
    {
        db.Devices.Add(new DeviceEntity
        {
            DeviceId = DeviceId,
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            MachineName = "PC-012",
            AssistanceRequestedAtUtc = CalledAt
        });
        db.Sessions.Add(new SessionEntity
        {
            SessionId = SessionId,
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            SeatId = Guid.NewGuid(),
            DeviceId = DeviceId,
            CreatedByStaffUserId = Guid.NewGuid(),
            PlayerKind = "guest",
            TariffRuleVersionId = "manual-v1",
            State = sessionState,
            RequestedAtUtc = CalledAt,
            StartedAtUtc = CalledAt,
            UpdatedAtUtc = CalledAt
        });
        var commandId = Guid.NewGuid();
        db.DeviceCommands.Add(new DeviceCommandEntity
        {
            CommandId = commandId,
            DeviceId = DeviceId,
            Type = DeviceCommandTypeNames.Lock,
            PayloadJson = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["sessionId"] = SessionId.ToString("D"),
                ["reason"] = "session-end"
            }),
            Status = lockStatus,
            CreatedAtUtc = Now.AddSeconds(-5),
            UpdatedAtUtc = Now.AddSeconds(-1)
        });
        await db.SaveChangesAsync();

        return commandId;
    }

    private sealed class ThrowingLeaseSigner : ISessionLeaseSigner
    {
        public SessionLeaseDto Sign(
            Guid SessionId, Guid OrganizationId, Guid BranchId, Guid SeatId, Guid DeviceId, string State,
            int Sequence, DateTimeOffset IssuedAtUtc, DateTimeOffset ExpiresAtUtc) =>
            throw new InvalidOperationException("No lease is expected when a session ends.");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
