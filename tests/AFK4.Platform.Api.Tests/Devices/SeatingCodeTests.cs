using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Devices;

/// <summary>
/// Код посадки, который простаивающий ПК показывает на своём мониторе.
///
/// Сегодня приложение получает список мест вместе с идентификаторами устройств и стартует сессию
/// по выбранному. Значит человек может занять свободную машину, не приходя в клуб: сервер видит
/// «игрок назвал устройство», и доказательства присутствия у него нет никакого. Код это
/// доказательство и есть — его видно только тому, кто смотрит на экран.
///
/// Отсюда всё остальное: код обязан жить минутами и меняться. Вечный код — это фотография в чате
/// «занимайте PC-07, вот код», то есть та же дыра с лишним шагом.
/// </summary>
public sealed class SeatingCodeTests
{
    private static readonly Guid OrgId = Guid.Parse("6f0a1c22-9a52-4a51-9a11-51ba2c000001");
    private static readonly Guid BranchId = Guid.Parse("6f0a1c22-9a52-4a51-9a11-51ba2c000002");
    private static readonly Guid DeviceId = Guid.Parse("6f0a1c22-9a52-4a51-9a11-51ba2c000003");
    private static readonly Guid OtherDeviceId = Guid.Parse("6f0a1c22-9a52-4a51-9a11-51ba2c000004");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-14T20:00:00Z");

    [Fact]
    public async Task Issue_GivesAShortCodeAndKeepsItForTheDevice()
    {
        await using var db = NewDb();
        var service = NewService(db, Now);

        var issued = await service.IssueAsync(OrgId, DeviceId, CancellationToken.None);

        Assert.NotNull(issued);
        // Код набирают с монитора на телефоне: шесть цифр — столько, сколько человек удерживает
        // в голове, пока переводит взгляд.
        Assert.Equal(6, issued!.Code.Length);
        Assert.All(issued.Code, character => Assert.True(char.IsAsciiDigit(character)));
        Assert.Equal(Now.Add(SeatingCodePolicy.Lifetime), issued.ExpiresAtUtc);
    }

    /// <summary>
    /// Пока код жив, ПК показывает один и тот же: перерисовывать монитор на каждый вопрос значит
    /// заставить человека набирать движущуюся мишень.
    /// </summary>
    [Fact]
    public async Task Issue_WhileTheCodeIsAlive_ReturnsTheSameOne()
    {
        await using var db = NewDb();

        var first = await NewService(db, Now).IssueAsync(OrgId, DeviceId, CancellationToken.None);
        var second = await NewService(db, Now.AddSeconds(30)).IssueAsync(OrgId, DeviceId, CancellationToken.None);

        Assert.Equal(first!.Code, second!.Code);
    }

    [Fact]
    public async Task Issue_AfterTheCodeDied_GivesANewOne()
    {
        await using var db = NewDb();

        var first = await NewService(db, Now).IssueAsync(OrgId, DeviceId, CancellationToken.None);
        var second = await NewService(db, Now.Add(SeatingCodePolicy.Lifetime).AddSeconds(1))
            .IssueAsync(OrgId, DeviceId, CancellationToken.None);

        Assert.NotEqual(first!.Code, second!.Code);
    }

    [Fact]
    public async Task Redeem_FindsTheDeviceInFrontOfThePerson()
    {
        await using var db = NewDb();
        var issued = await NewService(db, Now).IssueAsync(OrgId, DeviceId, CancellationToken.None);

        var found = await NewService(db, Now.AddSeconds(20))
            .RedeemAsync(OrgId, issued!.Code, CancellationToken.None);

        Assert.Equal(DeviceId, found);
    }

    /// <summary>
    /// Истёкший код — не «почти годный». Иначе достаточно сфотографировать монитор и занять
    /// машину из дома, а вся затея теряет смысл.
    /// </summary>
    [Fact]
    public async Task Redeem_AfterExpiry_FindsNothing()
    {
        await using var db = NewDb();
        var issued = await NewService(db, Now).IssueAsync(OrgId, DeviceId, CancellationToken.None);

        var found = await NewService(db, Now.Add(SeatingCodePolicy.Lifetime).AddSeconds(1))
            .RedeemAsync(OrgId, issued!.Code, CancellationToken.None);

        Assert.Null(found);
    }

    /// <summary>
    /// Код чужого клуба не работает даже совпав цифрами: шестизначных кодов немного, и в сети из
    /// двадцати клубов совпадения — вопрос времени, а не удачи.
    /// </summary>
    [Fact]
    public async Task Redeem_WithACodeFromAnotherClub_FindsNothing()
    {
        await using var db = NewDb();
        var issued = await NewService(db, Now).IssueAsync(OrgId, DeviceId, CancellationToken.None);

        var found = await NewService(db, Now.AddSeconds(20))
            .RedeemAsync(Guid.NewGuid(), issued!.Code, CancellationToken.None);

        Assert.Null(found);
    }

    /// <summary>Два ПК одного клуба не могут показывать один код: иначе сядешь не за тот.</summary>
    [Fact]
    public async Task Issue_ForTwoDevices_GivesDifferentCodes()
    {
        await using var db = NewDb();
        var service = NewService(db, Now);

        var first = await service.IssueAsync(OrgId, DeviceId, CancellationToken.None);
        var second = await service.IssueAsync(OrgId, OtherDeviceId, CancellationToken.None);

        Assert.NotEqual(first!.Code, second!.Code);
    }

    /// <summary>Пробелы и дефисы, которыми человек разбивает цифры, не должны мешать.</summary>
    [Fact]
    public async Task Redeem_IgnoresHowThePersonTypedIt()
    {
        await using var db = NewDb();
        var issued = await NewService(db, Now).IssueAsync(OrgId, DeviceId, CancellationToken.None);
        var typed = $" {issued!.Code[..3]}-{issued.Code[3..]} ";

        var found = await NewService(db, Now.AddSeconds(20)).RedeemAsync(OrgId, typed, CancellationToken.None);

        Assert.Equal(DeviceId, found);
    }

    private static PlatformDbContext NewDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static EfSeatingCodeService NewService(PlatformDbContext db, DateTimeOffset now) =>
        new(db, new FixedTimeProvider(now));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
