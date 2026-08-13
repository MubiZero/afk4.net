using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Players;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFK4.Platform.Api.Tests;

/// Стаж считается из истории визитов, а не хранится отдельно: отдельная таблица разошлась бы
/// с историей на первом же исправлении визита задним числом.
public sealed class PlayerAchievementsProjectorTests
{
    private static readonly Guid PlayerId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly DateTimeOffset Day = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

    private static PlatformDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"achievements-{Guid.NewGuid()}")
            .Options;
        var db = new PlatformDbContext(options);
        db.Branches.Add(new BranchEntity
        {
            BranchId = BranchId,
            OrganizationId = Guid.NewGuid(),
            Slug = "main",
            Name = "На Рудаки",
            City = "Душанбе",
            PreferredTimeZone = "Asia/Dushanbe",
            CreatedAtUtc = Day
        });
        return db;
    }

    private static void AddVisit(PlatformDbContext db, DateTimeOffset startedAtUtc, TimeSpan length)
    {
        db.Sessions.Add(new SessionEntity
        {
            SessionId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            BranchId = BranchId,
            SeatId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            CreatedByStaffUserId = Guid.NewGuid(),
            PlayerKind = "account",
            PlayerAccountId = PlayerId,
            State = SessionStateNames.Ended,
            RequestedAtUtc = startedAtUtc,
            StartedAtUtc = startedAtUtc,
            EndedAtUtc = startedAtUtc + length
        });
    }

    private static PlayerAchievementDto Achievement(PlayerAchievementsDto dto, string code) =>
        dto.Achievements.Single(achievement => achievement.Code == code);

    [Fact]
    public async Task NewPlayer_HasFirstLevelAndNothingUnlocked()
    {
        await using var db = NewContext();
        await db.SaveChangesAsync();

        var dto = await PlayerAchievementsProjector.GetAsync(db, PlayerId, CancellationToken.None);

        Assert.Equal(1, dto.Level);
        Assert.Equal(0, dto.VisitCount);
        Assert.Equal(0, dto.PlayedMinutes);
        Assert.All(dto.Achievements, achievement => Assert.Null(achievement.UnlockedAtUtc));
    }

    [Fact]
    public async Task PlayedHours_AddUpToMinutesAndLevel()
    {
        await using var db = NewContext();
        for (var index = 0; index < 3; index++)
        {
            AddVisit(db, Day.AddDays(index).AddHours(12), TimeSpan.FromHours(2));
        }

        await db.SaveChangesAsync();

        var dto = await PlayerAchievementsProjector.GetAsync(db, PlayerId, CancellationToken.None);

        Assert.Equal(3, dto.VisitCount);
        Assert.Equal(360, dto.PlayedMinutes);
        // Шесть часов — это второй порог (5 часов) пройден, третий (15) ещё нет.
        Assert.Equal(2, dto.Level);
        Assert.Equal(15 * 60 - 360, dto.MinutesToNextLevel);
        Assert.NotNull(Achievement(dto, PlayerAchievementCodes.FirstVisit).UnlockedAtUtc);
        Assert.Equal(3, Achievement(dto, PlayerAchievementCodes.Regular).Progress);
    }

    /// Пятичасовой визит — это марафон, и он виден сразу, а не после десятого прихода.
    [Fact]
    public async Task LongSingleVisit_UnlocksMarathon()
    {
        await using var db = NewContext();
        AddVisit(db, Day.AddHours(12), TimeSpan.FromHours(6));
        await db.SaveChangesAsync();

        var dto = await PlayerAchievementsProjector.GetAsync(db, PlayerId, CancellationToken.None);

        Assert.NotNull(Achievement(dto, PlayerAchievementCodes.Marathon).UnlockedAtUtc);
    }

    /// Ночь считается по часам клуба: 18:00 UTC — это 23:00 в Душанбе, и это ночной визит.
    /// По UTC он выглядел бы вечерним, и «ночного жителя» не получил бы никто.
    [Fact]
    public async Task NightVisits_AreCountedInClubHours()
    {
        await using var db = NewContext();
        for (var index = 0; index < 5; index++)
        {
            AddVisit(db, Day.AddDays(index).AddHours(18), TimeSpan.FromHours(1));
        }

        await db.SaveChangesAsync();

        var dto = await PlayerAchievementsProjector.GetAsync(db, PlayerId, CancellationToken.None);

        var nightOwl = Achievement(dto, PlayerAchievementCodes.NightOwl);
        Assert.Equal(5, nightOwl.Progress);
        Assert.NotNull(nightOwl.UnlockedAtUtc);
    }

    /// Дневные визиты ночными не становятся: 8 утра по клубу — это утро.
    [Fact]
    public async Task DaytimeVisits_DoNotCountAsNight()
    {
        await using var db = NewContext();
        for (var index = 0; index < 5; index++)
        {
            AddVisit(db, Day.AddDays(index).AddHours(3), TimeSpan.FromHours(1));
        }

        await db.SaveChangesAsync();

        var dto = await PlayerAchievementsProjector.GetAsync(db, PlayerId, CancellationToken.None);

        Assert.Equal(0, Achievement(dto, PlayerAchievementCodes.NightOwl).Progress);
    }

    /// Незакрытый визит — ещё не стаж: время идёт, и засчитывать его нечем.
    [Fact]
    public async Task RunningVisit_IsNotCountedYet()
    {
        await using var db = NewContext();
        db.Sessions.Add(new SessionEntity
        {
            SessionId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            BranchId = BranchId,
            SeatId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            CreatedByStaffUserId = Guid.NewGuid(),
            PlayerKind = "account",
            PlayerAccountId = PlayerId,
            State = SessionStateNames.Active,
            RequestedAtUtc = Day,
            StartedAtUtc = Day,
            EndedAtUtc = null
        });
        await db.SaveChangesAsync();

        var dto = await PlayerAchievementsProjector.GetAsync(db, PlayerId, CancellationToken.None);

        Assert.Equal(0, dto.VisitCount);
    }

    [Fact]
    public async Task WrittenReview_UnlocksReviewer()
    {
        await using var db = NewContext();
        AddVisit(db, Day.AddHours(12), TimeSpan.FromHours(1));
        db.ClubReviews.Add(new ClubReviewEntity
        {
            ReviewId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            BranchId = BranchId,
            PlayerAccountId = PlayerId,
            SessionId = Guid.NewGuid(),
            Rating = 5,
            CreatedAtUtc = Day.AddHours(14)
        });
        await db.SaveChangesAsync();

        var dto = await PlayerAchievementsProjector.GetAsync(db, PlayerId, CancellationToken.None);

        Assert.NotNull(Achievement(dto, PlayerAchievementCodes.Reviewer).UnlockedAtUtc);
    }

    /// Чужие визиты в чужой стаж не попадают.
    [Fact]
    public async Task OtherPlayersVisits_AreNotCounted()
    {
        await using var db = NewContext();
        AddVisit(db, Day.AddHours(12), TimeSpan.FromHours(2));
        db.Sessions.Add(new SessionEntity
        {
            SessionId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            BranchId = BranchId,
            SeatId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            CreatedByStaffUserId = Guid.NewGuid(),
            PlayerKind = "account",
            PlayerAccountId = Guid.NewGuid(),
            State = SessionStateNames.Ended,
            RequestedAtUtc = Day,
            StartedAtUtc = Day,
            EndedAtUtc = Day.AddHours(9)
        });
        await db.SaveChangesAsync();

        var dto = await PlayerAchievementsProjector.GetAsync(db, PlayerId, CancellationToken.None);

        Assert.Equal(1, dto.VisitCount);
        Assert.Equal(120, dto.PlayedMinutes);
    }
}
