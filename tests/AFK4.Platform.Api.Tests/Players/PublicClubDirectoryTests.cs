using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AFK4.Platform.Api.Tests.Players;

/// <summary>
/// Общий список клубов открывает каждый игрок, выбирающий клуб, — его считают один раз на полминуты.
/// Поиск идёт мимо: строк поиска бесконечно много, держать их в памяти незачем.
/// </summary>
public sealed class PublicClubDirectoryTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-26T12:00:00Z");

    [Fact]
    public async Task TheListIsSharedForHalfAMinute_ButASearchIsAlwaysFresh()
    {
        await using var db = NewContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = new PublicClubDirectoryOptions();
        AddClub(db, "CyberX");
        await db.SaveChangesAsync();

        var first = await PublicClubDirectory.GetAsync(db, cache, options, null, Now, CancellationToken.None);
        AddClub(db, "Arena");
        await db.SaveChangesAsync();
        var second = await PublicClubDirectory.GetAsync(db, cache, options, "  ", Now, CancellationToken.None);
        var searched = await PublicClubDirectory.GetAsync(db, cache, options, "arena", Now, CancellationToken.None);

        Assert.Same(first, second);
        Assert.Single(second);
        Assert.Equal("Arena", Assert.Single(searched).Name);
    }

    [Fact]
    public async Task TheCheapestActiveTariff_IsThePriceFrom()
    {
        await using var db = NewContext();
        var organizationId = AddClub(db, "CyberX");
        db.TariffVersions.AddRange(
            Tariff(organizationId, 1_000, retired: false),
            Tariff(organizationId, 500, retired: true),
            Tariff(organizationId, 800, retired: false));
        await db.SaveChangesAsync();

        var entry = Assert.Single(await PublicClubDirectory.GetAsync(
            db, new MemoryCache(new MemoryCacheOptions()), new PublicClubDirectoryOptions { ListCacheDuration = TimeSpan.Zero },
            null, Now, CancellationToken.None));

        // Снятая с публикации версия дешевле, но на кассе её никто не назовёт.
        Assert.Equal(800 * 60, entry.PricePerHourFromMinorUnits);
        Assert.Equal("TJS", entry.CurrencyCode);
    }

    private static PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>().UseInMemoryDatabase($"directory-{Guid.NewGuid()}").Options);

    private static Guid AddClub(PlatformDbContext db, string name)
    {
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = organizationId, Slug = name.ToLowerInvariant(), Name = name, Status = "active", CreatedAtUtc = Now
        });
        return organizationId;
    }

    private static TariffVersionEntity Tariff(Guid organizationId, long pricePerMinute, bool retired) => new()
    {
        TariffVersionId = Guid.NewGuid(),
        OrganizationId = organizationId,
        BranchId = Guid.NewGuid(),
        PricePerMinuteMinorUnits = pricePerMinute,
        CurrencyCode = "TJS",
        EffectiveFromUtc = Now.AddDays(-1),
        RetiredAtUtc = retired ? Now.AddHours(-1) : null
    };
}
