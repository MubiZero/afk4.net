using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class EfSessionTimelineReadServiceTests
{
    private static readonly Guid OrgId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid ZoneId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SeatId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PlayerId = Guid.Parse("cccccccc-cccc-4ccc-cccc-cccccccccccc");

    private static SessionEntity Session(Guid id, DateTimeOffset? startedAt, DateTimeOffset? endsAt, DateTimeOffset? endedAt, string state, Guid? playerId = null)
        => new()
        {
            SessionId = id,
            OrganizationId = OrgId,
            BranchId = BranchId,
            SeatId = SeatId,
            DeviceId = Guid.NewGuid(),
            CreatedByStaffUserId = Guid.NewGuid(),
            PlayerKind = playerId is null ? "guest" : "player",
            PlayerAccountId = playerId,
            TariffRuleVersionId = "guest",
            State = state,
            RequestedAtUtc = (startedAt ?? endedAt ?? DateTimeOffset.UtcNow).AddMinutes(-5),
            StartedAtUtc = startedAt,
            EndsAtUtc = endsAt,
            EndedAtUtc = endedAt,
            UpdatedAtUtc = endedAt ?? startedAt ?? DateTimeOffset.UtcNow
        };

    [Fact]
    public async Task GetSessionsAsync_ProjectsStartedSessionsOverlappingWindow_WithNamesAndOpenEnd()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var dayStart = DateTimeOffset.Parse("2026-06-18T00:00:00Z");
        var dayEnd = dayStart.AddDays(1);
        var closedId = Guid.Parse("11111111-1111-4111-1111-111111111111");
        var openId = Guid.Parse("22222222-2222-4222-2222-222222222222");

        await using (var db = new PlatformDbContext(options))
        {
            db.Zones.Add(new ZoneEntity { ZoneId = ZoneId, OrganizationId = OrgId, BranchId = BranchId, Name = "Зал A", SortOrder = 1, CreatedAtUtc = dayStart });
            db.Seats.Add(new SeatEntity { SeatId = SeatId, OrganizationId = OrgId, BranchId = BranchId, ZoneId = ZoneId, Name = "PC-01", SortOrder = 10, CreatedAtUtc = dayStart });
            db.PlayerAccounts.Add(new PlayerAccountEntity
            {
                PlayerAccountId = PlayerId, OrganizationId = OrgId, HomeBranchId = BranchId,
                DisplayName = "Амир К.", PhoneNumber = "+992900000001", PreferredLocale = "ru",
                MarketingOptIn = false, IsActive = true, CreatedAtUtc = dayStart
            });

            // Closed session 04:00–05:00 (history) with a named player.
            db.Sessions.Add(Session(closedId, dayStart.AddHours(4), null, dayStart.AddHours(5), "ended", PlayerId));
            // Open tab started 09:00 — no scheduled and no actual end.
            db.Sessions.Add(Session(openId, dayStart.AddHours(9), null, null, "active"));
            // Out of window: ended yesterday.
            db.Sessions.Add(Session(Guid.NewGuid(), dayStart.AddHours(-20), null, dayStart.AddHours(-19), "ended"));
            // Requested but never started — no timeline span.
            db.Sessions.Add(Session(Guid.NewGuid(), null, null, null, "requested"));
            await db.SaveChangesAsync();
        }

        await using (var db = new PlatformDbContext(options))
        {
            var service = new EfSessionTimelineReadService(db);
            var result = await service.GetSessionsAsync(OrgId, BranchId, dayStart, dayEnd, CancellationToken.None);

            Assert.Equal(2, result.Sessions.Count);
            var ordered = result.Sessions.OrderBy(s => s.StartedAtUtc).ToList();

            // Closed history session first, fully bounded with resolved seat/zone/player names.
            Assert.Equal(closedId, ordered[0].SessionId);
            Assert.Equal(dayStart.AddHours(4), ordered[0].StartedAtUtc);
            Assert.Equal(dayStart.AddHours(5), ordered[0].EndedAtUtc);
            Assert.Equal("PC-01", ordered[0].SeatName);
            Assert.Equal("Зал A", ordered[0].ZoneName);
            Assert.Equal("Амир К.", ordered[0].PlayerDisplayName);

            // Open tab second — no end at all (UI renders open-ended).
            Assert.Equal(openId, ordered[1].SessionId);
            Assert.Null(ordered[1].EndsAtUtc);
            Assert.Null(ordered[1].EndedAtUtc);
            Assert.Null(ordered[1].PlayerDisplayName);
        }
    }
}
