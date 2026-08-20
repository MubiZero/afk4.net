using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Platform.Analytics;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

/// <summary>
/// Переход на сетевой PIN: чем о нём объявляют и по какому числу считают законченным.
///
/// Оба маршрута существуют ради конца перехода. Без объявления люди узнают о смене правил у ПК, а
/// без числа «уже задали» переход не кончается никогда и вечный режим совместимости остаётся
/// навсегда — именно этого мы и не хотим.
/// </summary>
public sealed class PinTransitionEndpointTests
{
    // Окно показателя считается от «сейчас», поэтому и данные теста живут вокруг настоящего «сейчас».
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task Announcement_ReachesOnlyThoseWithAnAppAndWithoutAPin()
    {
        await using var factory = new PlatformApiFactory();
        var club = await SeedClubAsync(factory);

        var withoutPin = await SeedPlayerAsync(factory, club, "+992900000801", hasPin: false, hasDevice: true);
        var withPin = await SeedPlayerAsync(factory, club, "+992900000802", hasPin: true, hasDevice: true);
        var withoutApp = await SeedPlayerAsync(factory, club, "+992900000803", hasPin: false, hasDevice: false);

        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.PostAsync("/api/platform/announcements/pin-migration/push", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var queued = await db.NotificationOutbox
            .Where(row => row.TemplateKey == NotificationTemplateKeys.PlayerPinMigration)
            .ToListAsync();

        Assert.Single(queued);
        Assert.Equal(withoutPin, queued[0].PlayerAccountId);
        Assert.DoesNotContain(queued, row => row.PlayerAccountId == withPin);
        Assert.DoesNotContain(queued, row => row.PlayerAccountId == withoutApp);
    }

    // Пуш будит телефон. Второе нажатие кнопки не имеет права разбудить его ещё раз.
    [Fact]
    public async Task Announcement_SentTwice_WakesNobodyTwice()
    {
        await using var factory = new PlatformApiFactory();
        var club = await SeedClubAsync(factory);
        await SeedPlayerAsync(factory, club, "+992900000804", hasPin: false, hasDevice: true);

        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        await client.PostAsync("/api/platform/announcements/pin-migration/push", null);
        await client.PostAsync("/api/platform/announcements/pin-migration/push", null);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Single(await db.NotificationOutbox
            .Where(row => row.TemplateKey == NotificationTemplateKeys.PlayerPinMigration)
            .ToListAsync());
    }

    [Fact]
    public async Task Announcement_WithoutPermission_IsRefused()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: []);

        var response = await client.PostAsync("/api/platform/announcements/pin-migration/push", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PinAdoption_CountsOnlyPlayersWhoCameLatelyAndCanHaveAPinAtAll()
    {
        await using var factory = new PlatformApiFactory();
        var club = await SeedClubAsync(factory);

        var setPin = await SeedPlayerAsync(factory, club, "+992900000811", hasPin: true, hasDevice: false);
        var noPin = await SeedPlayerAsync(factory, club, "+992900000812", hasPin: false, hasDevice: false);
        var deskGuest = await SeedDeskGuestAsync(factory, club);
        var longGone = await SeedPlayerAsync(factory, club, "+992900000813", hasPin: false, hasDevice: false);

        await AddSessionAsync(factory, club, setPin, Now.AddDays(-2));
        await AddSessionAsync(factory, club, noPin, Now.AddDays(-3));
        await AddSessionAsync(factory, club, deskGuest, Now.AddDays(-4));
        await AddSessionAsync(factory, club, longGone, Now.AddDays(-200));

        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var adoption = await client.GetFromJsonAsync<PinAdoptionDto>(
            "/api/platform/analytics/pin-adoption?windowDays=30");

        Assert.NotNull(adoption);
        Assert.Equal(30, adoption!.WindowDays);
        // Трое приходили за окно; гость, заведённый на стойке, PIN задать не может вовсе, поэтому
        // в знаменатель доли он не попадает — иначе порог 90% недостижим по построению.
        Assert.Equal(3, adoption.ActivePlayers);
        Assert.Equal(2, adoption.ActivePlayersWithIdentity);
        Assert.Equal(1, adoption.ActivePlayersWithPin);
        Assert.Equal(50, adoption.AdoptionPercent);
    }

    [Fact]
    public async Task PinAdoption_WithoutAuthentication_IsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/platform/analytics/pin-adoption");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PinAdoption_WithoutPermission_IsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: []);

        var response = await client.GetAsync("/api/platform/analytics/pin-adoption");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<(Guid OrganizationId, Guid BranchId)> SeedClubAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = organizationId,
            Name = "Клуб перехода",
            CreatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = branchId,
            OrganizationId = organizationId,
            Name = "Филиал",
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return (organizationId, branchId);
    }

    private static async Task<Guid> SeedPlayerAsync(
        PlatformApiFactory factory,
        (Guid OrganizationId, Guid BranchId) club,
        string phone,
        bool hasPin,
        bool hasDevice)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var person = new PlatformPersonEntity
        {
            PlatformPersonId = Guid.NewGuid(),
            PhoneNumber = phone,
            DisplayName = "Игрок",
            PhoneVerifiedAtUtc = Now,
            IsActive = true,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        };
        if (hasPin)
        {
            person.PinHash = new PasswordHasher<PlatformPersonEntity>().HashPassword(person, "1234");
            person.PinSetAtUtc = Now;
        }

        db.PlatformPersons.Add(person);

        var playerAccountId = Guid.NewGuid();
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerAccountId,
            OrganizationId = club.OrganizationId,
            PlatformPersonId = person.PlatformPersonId,
            HomeBranchId = club.BranchId,
            DisplayName = "Игрок",
            PhoneNumber = phone,
            IsActive = true,
            CreatedAtUtc = Now
        });

        if (hasDevice)
        {
            db.PlayerDevices.Add(new PlayerDeviceEntity
            {
                PlayerDeviceId = Guid.NewGuid(),
                PlayerAccountId = playerAccountId,
                PushToken = "token-" + phone,
                Platform = "android",
                Locale = "ru",
                CreatedUtc = Now,
                LastSeenUtc = Now
            });
        }

        await db.SaveChangesAsync();
        return playerAccountId;
    }

    /// <summary>Карточка, заведённая на стойке: личности за ней нет, а значит и PIN быть не может.</summary>
    private static async Task<Guid> SeedDeskGuestAsync(
        PlatformApiFactory factory, (Guid OrganizationId, Guid BranchId) club)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var playerAccountId = Guid.NewGuid();
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerAccountId,
            OrganizationId = club.OrganizationId,
            HomeBranchId = club.BranchId,
            DisplayName = "Гость со стойки",
            IsActive = true,
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return playerAccountId;
    }

    private static async Task AddSessionAsync(
        PlatformApiFactory factory,
        (Guid OrganizationId, Guid BranchId) club,
        Guid playerAccountId,
        DateTimeOffset startedAtUtc)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.Sessions.Add(new SessionEntity
        {
            SessionId = Guid.NewGuid(),
            OrganizationId = club.OrganizationId,
            BranchId = club.BranchId,
            SeatId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            CreatedByStaffUserId = Guid.Empty,
            PlayerKind = "member",
            PlayerAccountId = playerAccountId,
            TariffRuleVersionId = "v1",
            BillingMode = "prepaid_wallet",
            State = SessionStateNames.Ended,
            RequestedAtUtc = startedAtUtc,
            StartedAtUtc = startedAtUtc,
            EndedAtUtc = startedAtUtc.AddHours(1),
            UpdatedAtUtc = startedAtUtc.AddHours(1)
        });
        await db.SaveChangesAsync();
    }
}
