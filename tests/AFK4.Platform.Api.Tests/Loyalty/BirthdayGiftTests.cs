using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Loyalty;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Platform.Api.Tests.Identity;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Loyalty;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Loyalty;

/// <summary>
/// Подарок на день рождения (владелец, 2026-09-26: «акции на др»): утром дня рождения — по часам
/// клуба — на баланс ложится подарок и приходит поздравление. Раз в год и только тем, кто ввёл дату
/// заранее и недавно был в клубе.
/// </summary>
public sealed class BirthdayGiftTests
{
    // 14 марта, 11:00 в Душанбе (UTC+5).
    private static readonly DateTimeOffset BirthdayMorning = DateTimeOffset.Parse("2026-03-14T06:00:00Z");
    private static readonly DateOnly BirthDate = new(2000, 3, 14);

    private sealed record Seeded(Guid OrganizationId, Guid PlayerAccountId);

    [Fact]
    public async Task OnTheBirthdayMorning_TheGiftLandsOnTheWallet_OnceAYear()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory);

        Assert.Equal(1, await RunAtAsync(factory, BirthdayMorning));
        Assert.Equal(0, await RunAtAsync(factory, BirthdayMorning.AddHours(3)));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var entry = await db.LedgerEntries.SingleAsync(e => e.PlayerAccountId == seeded.PlayerAccountId);
        Assert.Equal(LedgerEntryTypeNames.BirthdayBonus, entry.EntryType);
        Assert.Equal(LedgerAccountTypeNames.Wallet, entry.AccountType);
        Assert.Equal(2_000, entry.AmountMinorUnits);
        var gift = await db.PlayerBirthdayGifts.SingleAsync(g => g.PlayerAccountId == seeded.PlayerAccountId);
        Assert.Equal(2026, gift.Year);
        Assert.Equal(entry.LedgerEntryId, gift.LedgerEntryId);
        var push = await db.NotificationOutbox.SingleAsync(n => n.PlayerAccountId == seeded.PlayerAccountId);
        Assert.Equal(NotificationTemplateKeys.PlayerBirthdayGift, push.TemplateKey);
    }

    // Поздравление в полночь будит, а не радует; накануне — ещё не день рождения.
    [Fact]
    public async Task BeforeTenInTheClub_OrTheDayBefore_NothingIsGiven()
    {
        await using var factory = new PlatformApiFactory();
        await SeedAsync(factory);

        Assert.Equal(0, await RunAtAsync(factory, DateTimeOffset.Parse("2026-03-14T03:00:00Z"))); // 08:00 в Душанбе
        Assert.Equal(0, await RunAtAsync(factory, BirthdayMorning.AddDays(-1)));
    }

    // Дату вписали на днях — похоже на «сегодня ради подарка».
    [Fact]
    public async Task ABirthdayEnteredLastWeek_GetsNoGift()
    {
        await using var factory = new PlatformApiFactory();
        await SeedAsync(factory, birthDateEnteredDaysAgo: 5);

        Assert.Equal(0, await RunAtAsync(factory, BirthdayMorning));
    }

    [Fact]
    public async Task AGuestWhoHasNotComeForAYear_GetsNoGift_UnlessTheClubGivesToEveryone()
    {
        await using var factory = new PlatformApiFactory();
        await SeedAsync(factory, lastVisitDaysAgo: 400);
        Assert.Equal(0, await RunAtAsync(factory, BirthdayMorning));

        await using var other = new PlatformApiFactory();
        await SeedAsync(other, lastVisitDaysAgo: 400, recentVisitDays: 0);
        Assert.Equal(1, await RunAtAsync(other, BirthdayMorning));
    }

    [Fact]
    public async Task AClubWithTheGiftOff_GivesNothing()
    {
        await using var factory = new PlatformApiFactory();
        await SeedAsync(factory, enabled: false);

        Assert.Equal(0, await RunAtAsync(factory, BirthdayMorning));
    }

    [Fact]
    public async Task TheOwner_SetsTheGift_AndAGiftOfNothingIsRefused()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var route = $"/api/organizations/{TestIds.OrganizationId:D}/birthday-gift-settings";

        var initial = await client.GetFromJsonAsync<BirthdayGiftSettingsDto>(route);
        Assert.False(initial!.Enabled);
        Assert.Equal(BirthdayGifts.DefaultRecentVisitDays, initial.RecentVisitDays);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync(route, new UpdateBirthdayGiftSettingsRequest(true, 0, 180))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync(route, new UpdateBirthdayGiftSettingsRequest(true, 2_000, -1))).StatusCode);

        var saved = await client.PostAsJsonAsync(route, new UpdateBirthdayGiftSettingsRequest(true, 2_000, 90));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(new BirthdayGiftSettingsDto(true, 2_000, 90), await client.GetFromJsonAsync<BirthdayGiftSettingsDto>(route));
    }

    [Fact]
    public async Task AnOperator_DoesNotHandOutTheClubsMoney()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/birthday-gift-settings",
            new UpdateBirthdayGiftSettingsRequest(true, 100_000, 0));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<int> RunAtAsync(PlatformApiFactory factory, DateTimeOffset now)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var runner = new BirthdayGiftRunner(
            services.GetRequiredService<PlatformDbContext>(),
            services.GetRequiredService<IOrganizationEntitlements>(),
            services.GetRequiredService<PlayerPushNotifier>(),
            services.GetRequiredService<IOptions<BillingOptions>>(),
            new MovableTimeProvider(now),
            NullLogger<BirthdayGiftRunner>.Instance);
        return await runner.RunAsync(CancellationToken.None);
    }

    private static async Task<Seeded> SeedAsync(
        PlatformApiFactory factory,
        bool enabled = true,
        int recentVisitDays = 180,
        int birthDateEnteredDaysAgo = 60,
        int lastVisitDaysAgo = 10)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        db.Organizations.Add(new OrganizationEntity { OrganizationId = organizationId, Name = "CyberX", CreatedAtUtc = BirthdayMorning.AddYears(-1) });
        db.Branches.Add(new BranchEntity
        {
            BranchId = branchId, OrganizationId = organizationId, Slug = $"b{branchId:N}"[..12], Name = "На Рудаки",
            City = "Душанбе", PreferredTimeZone = "Asia/Dushanbe", CreatedAtUtc = BirthdayMorning.AddYears(-1)
        });
        db.OrganizationBirthdayGiftSettings.Add(new OrganizationBirthdayGiftSettingsEntity
        {
            OrganizationId = organizationId, Enabled = enabled, AmountMinorUnits = 2_000,
            RecentVisitDays = recentVisitDays, UpdatedAtUtc = BirthdayMorning.AddDays(-90)
        });
        db.PlatformPersons.Add(new PlatformPersonEntity
        {
            PlatformPersonId = personId, PhoneNumber = TestPhones.Next(), DisplayName = "Азиз",
            BirthDate = BirthDate, BirthDateSetAtUtc = BirthdayMorning.AddDays(-birthDateEnteredDaysAgo),
            IsActive = true, CreatedAtUtc = BirthdayMorning.AddYears(-1), UpdatedAtUtc = BirthdayMorning.AddYears(-1)
        });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = accountId, OrganizationId = organizationId, PlatformPersonId = personId,
            HomeBranchId = branchId, DisplayName = "Азиз", IsActive = true, CreatedAtUtc = BirthdayMorning.AddYears(-1)
        });
        db.Sessions.Add(new SessionEntity
        {
            SessionId = Guid.NewGuid(), OrganizationId = organizationId, BranchId = branchId, SeatId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(), PlayerAccountId = accountId, PlayerKind = "player", State = "Ended",
            RequestedAtUtc = BirthdayMorning.AddDays(-lastVisitDaysAgo),
            StartedAtUtc = BirthdayMorning.AddDays(-lastVisitDaysAgo)
        });
        await db.SaveChangesAsync();
        return new Seeded(organizationId, accountId);
    }
}
