using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Notifications;

/// <summary>Язык уведомлений — человека: сменил его в приложении — пуши любого клуба приходят на нём.</summary>
public sealed class PlayerNotificationLocaleTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-27T10:00:00Z");

    [Theory]
    [InlineData("tg", "ru", "tg")]
    [InlineData(null, "ru", "ru")]
    [InlineData(null, null, "")]
    public async Task ThePersonsLanguageWins_TheClubCardIsTheFallback(string? personLocale, string? accountLocale, string expected)
    {
        await using var db = new PlatformDbContext(
            new DbContextOptionsBuilder<PlatformDbContext>().UseInMemoryDatabase($"locale-{Guid.NewGuid()}").Options);
        var personId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        db.PlatformPersons.Add(new PlatformPersonEntity
        {
            PlatformPersonId = personId, PhoneNumber = "+992900000001", DisplayName = "Азиз",
            PreferredLocale = personLocale, CreatedAtUtc = Now, UpdatedAtUtc = Now
        });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = accountId, OrganizationId = Guid.NewGuid(), HomeBranchId = Guid.NewGuid(),
            PlatformPersonId = personId, DisplayName = "Азиз", PreferredLocale = accountLocale, CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();

        Assert.Equal(expected, await PlayerNotificationLocale.ResolveAsync(db, accountId, CancellationToken.None));
    }
}
