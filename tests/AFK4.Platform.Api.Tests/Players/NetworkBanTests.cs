using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Players;

/// <summary>
/// Сетевой запрет: платформа закрывает человеку самообслуживание во всей сети.
///
/// До сих пор поле <c>NetworkBanAtUtc</c> читалось в четырёх местах и не записывалось ни в одном,
/// а закрывало ровно одну дверь из двух — вход по PIN. Человек с запретом входил по SMS и играл
/// дальше, пока стойка видела в его карточке «запрещён в сети». Полу-запрет хуже отсутствия
/// запрета: клуб верит цифре, а цифра не значит ничего.
///
/// Устройство запрета: он останавливает действия, а не зрение. Деньги на кошельке — деньги
/// человека, и посмотреть на них он вправе; начать на них новое — уже нет.
/// </summary>
public sealed class NetworkBanTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task BannedPerson_CannotStartAnythingNew()
    {
        await using var factory = new PlatformApiFactory();
        var player = await SeedPlayerAsync(factory);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, player);
        await BanAsync(factory, player, "Подобрал чужой кошелёк");

        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent", new PlayerTopUpIntentRequest(5_000, "TJS", "counter"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(
            "network_banned", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Бронь — то же самое действие с другой стороны: запрет не список маршрутов, а правило.
    /// </summary>
    [Fact]
    public async Task BannedPerson_CannotBook()
    {
        await using var factory = new PlatformApiFactory();
        var player = await SeedPlayerAsync(factory);
        var seatId = await SeedSeatAsync(factory, player.OrgId, player.BranchId);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, player);
        await BanAsync(factory, player, "Подобрал чужой кошелёк");

        var startsAt = Now.AddHours(2);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(seatId, startsAt, startsAt.AddHours(1), null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(
            "network_banned", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Деньги на кошельке остаются деньгами человека. Отобрать у него ещё и зрение значит
    /// превратить запрет в пропажу: приложение молчит, а сумма недоступна.
    /// </summary>
    [Fact]
    public async Task BannedPerson_StillSeesHimselfAndHisMoney()
    {
        await using var factory = new PlatformApiFactory();
        var player = await SeedPlayerAsync(factory);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, player);
        await BanAsync(factory, player, "Подобрал чужой кошелёк");

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<MeDto>();
        Assert.True(me!.Person.NetworkBanned);
    }

    /// <summary>
    /// Отменить свою бронь запрещённому можно и нужно: отмена освобождает и место клуба, и
    /// замороженные деньги человека. Запрет останавливает начатое им, а не возвращённое обратно.
    /// </summary>
    [Fact]
    public async Task BannedPerson_CanStillCancelWhatHeStartedBefore()
    {
        await using var factory = new PlatformApiFactory();
        var player = await SeedPlayerAsync(factory);
        var seatId = await SeedSeatAsync(factory, player.OrgId, player.BranchId);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, player);

        var startsAt = Now.AddHours(2);
        var booking = await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(seatId, startsAt, startsAt.AddHours(1), null));
        Assert.Equal(HttpStatusCode.OK, booking.StatusCode);
        var reservation = await booking.Content.ReadFromJsonAsync<PlayerReservationDto>();

        await BanAsync(factory, player, "Подобрал чужой кошелёк");
        var cancelled = await client.DeleteAsync($"/api/me/reservations/{reservation!.ReservationId}");

        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
    }

    /// <summary>
    /// Дверь на вход отвечает запрещённому ровно то же, что незапрещённому. Иначе перебор номеров
    /// на экране игрового ПК становится справочником «кто в этой сети под запретом», то есть
    /// заодно и справочником «кто в этой сети вообще есть».
    /// </summary>
    [Fact]
    public async Task TheDoorDoesNotTellWhoIsBanned()
    {
        await using var factory = new PlatformApiFactory();
        var player = await SeedPlayerAsync(factory);
        await BanAsync(factory, player, "Подобрал чужой кошелёк");
        using var client = factory.CreateClient();

        var signIn = await client.PostAsJsonAsync(
            "/api/public/player/sign-in",
            new PlayerSignInRequest(player.OrgId, player.Phone, PlayerPinTestData.DefaultPin));

        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    /// <summary>
    /// Запрет снимается — и человек снова действует. Иначе снятие было бы обещанием без силы.
    /// </summary>
    [Fact]
    public async Task LiftedBan_ReturnsTheRightToAct()
    {
        await using var factory = new PlatformApiFactory();
        var player = await SeedPlayerAsync(factory);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, player);
        await BanAsync(factory, player, "Подобрал чужой кошелёк");
        await LiftAsync(factory, player);

        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent", new PlayerTopUpIntentRequest(5_000, "TJS", "counter"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed record SeededPlayer(Guid OrgId, Guid BranchId, Guid PlayerAccountId, string Phone);

    private static async Task<SeededPlayer> SeedPlayerAsync(PlatformApiFactory factory)
    {
        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var playerAccountId = Guid.NewGuid();
        var phone = TestPhones.Next();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            db.Organizations.Add(new OrganizationEntity
            {
                OrganizationId = org,
                Name = "Клуб под запретом",
                CreatedAtUtc = Now
            });
            db.PlayerAccounts.Add(new PlayerAccountEntity
            {
                PlayerAccountId = playerAccountId,
                OrganizationId = org,
                HomeBranchId = branch,
                DisplayName = "Фаррух",
                PhoneNumber = phone,
                PreferredLocale = "ru",
                IsActive = true,
                CreatedAtUtc = Now
            });
            db.BranchBookingSettings.Add(BranchBookingSettingsTestData.AcceptsAnyGuest(org, branch, Now));
            await db.SaveChangesAsync();
        }

        await PlayerPinTestData.AttachPersonWithPinAsync(factory, playerAccountId, phone);
        return new SeededPlayer(org, branch, playerAccountId, phone);
    }

    private static async Task<Guid> SeedSeatAsync(PlatformApiFactory factory, Guid orgId, Guid branchId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var zoneId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        db.Zones.Add(new ZoneEntity
        {
            ZoneId = zoneId,
            OrganizationId = orgId,
            BranchId = branchId,
            Name = "Зал A",
            SortOrder = 10,
            CreatedAtUtc = Now
        });
        db.Seats.Add(new SeatEntity
        {
            SeatId = seatId,
            OrganizationId = orgId,
            BranchId = branchId,
            ZoneId = zoneId,
            Name = "PC-01",
            SortOrder = 10,
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return seatId;
    }

    private static async Task AuthenticateAsync(HttpClient client, SeededPlayer player)
    {
        var signIn = await client.PostAsJsonAsync(
            "/api/public/player/sign-in",
            new PlayerSignInRequest(player.OrgId, player.Phone, PlayerPinTestData.DefaultPin));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    private static async Task BanAsync(PlatformApiFactory factory, SeededPlayer player, string reason)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var person = await db.PlatformPersons.SingleAsync(
            candidate => candidate.PhoneNumber == player.Phone);
        person.NetworkBanAtUtc = Now;
        person.NetworkBanReason = reason;
        await db.SaveChangesAsync();
    }

    private static async Task LiftAsync(PlatformApiFactory factory, SeededPlayer player)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var person = await db.PlatformPersons.SingleAsync(
            candidate => candidate.PhoneNumber == player.Phone);
        person.NetworkBanAtUtc = null;
        person.NetworkBanReason = null;
        await db.SaveChangesAsync();
    }
}
