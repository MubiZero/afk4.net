using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Friends;
using AFK4.Shared.Contracts.Players;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests.Friends;

/// <summary>
/// Друзья по HTTP: двери. Правила самой дружбы проверяет <see cref="EfFriendServiceTests"/>.
/// </summary>
public sealed class FriendEndpointTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed record SeededPerson(Guid OrgId, Guid PlayerId, string Phone);

    private static async Task<SeededPerson> SeedPersonAsync(PlatformApiFactory factory, string pin)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var player = Guid.NewGuid();
        var phone = TestPhones.Next();

        db.Branches.Add(new BranchEntity
        {
            BranchId = branch,
            OrganizationId = org,
            Slug = "main",
            Name = "На Рудаки",
            City = "Душанбе",
            CreatedAtUtc = Now
        });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = player,
            OrganizationId = org,
            HomeBranchId = branch,
            DisplayName = "Игрок",
            PhoneNumber = phone,
            PreferredLocale = "ru",
            IsActive = true,
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        await PlayerPinTestData.AttachPersonWithPinAsync(factory, player, phone, pin);
        return new SeededPerson(org, player, phone);
    }

    private static async Task AuthenticateAsync(HttpClient client, SeededPerson person, string pin)
    {
        var signIn = await client.PostAsJsonAsync(
            "/api/public/player/sign-in", new PlayerSignInRequest(person.OrgId, person.Phone, pin));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    [Fact]
    public async Task Friends_WithoutSignIn_AreRefused()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/me/friends");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Весь путь одним заходом: позвал — принял — увидели друг друга.
    [Fact]
    public async Task Request_Accept_AndBothSeeEachOther()
    {
        await using var factory = new PlatformApiFactory();
        var me = await SeedPersonAsync(factory, "1234");
        var friend = await SeedPersonAsync(factory, "4321");

        using var myClient = factory.CreateClient();
        await AuthenticateAsync(myClient, me, "1234");
        using var friendClient = factory.CreateClient();
        await AuthenticateAsync(friendClient, friend, "4321");

        var sent = await myClient.PostAsJsonAsync(
            "/api/me/friends/requests", new SendFriendRequestRequest(friend.Phone));
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);

        var incoming = await friendClient.GetFromJsonAsync<FriendsDto>("/api/me/friends");
        var request = Assert.Single(incoming!.Incoming);

        var accepted = await friendClient.PostAsync(
            $"/api/me/friends/requests/{request.FriendRequestId:D}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        var mine = await myClient.GetFromJsonAsync<FriendsDto>("/api/me/friends");
        Assert.Single(mine!.Friends);
    }

    /// Заявка по номеру, которого в сети нет, отвечает ровно тем же, чем по настоящему —
    /// включая тело ответа. Иначе приложение стало бы способом проверять чужие номера.
    [Fact]
    public async Task Request_ToAnUnknownNumber_LooksExactlyLikeARealOne()
    {
        await using var factory = new PlatformApiFactory();
        var me = await SeedPersonAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, me, "1234");

        var toNobody = await client.PostAsJsonAsync(
            // Номер, которого в сети нет: у TestPhones префикс «+9929», поэтому берём другой.
            "/api/me/friends/requests", new SendFriendRequestRequest("+992559999999"));
        var toNobodyBody = await toNobody.Content.ReadAsStringAsync();

        var friend = await SeedPersonAsync(factory, "4321");
        var toReal = await client.PostAsJsonAsync(
            "/api/me/friends/requests", new SendFriendRequestRequest(friend.Phone));
        var toRealBody = await toReal.Content.ReadAsStringAsync();

        Assert.Equal(toReal.StatusCode, toNobody.StatusCode);
        // Тела различаются только тем, что во втором случае появилась отправленная заявка —
        // сам факт «такой номер есть» из ответа не читается ни в одном из них.
        Assert.DoesNotContain(friend.Phone, toRealBody, StringComparison.Ordinal);
        Assert.DoesNotContain("992900000001", toNobodyBody, StringComparison.Ordinal);
    }

    // Чужую заявку по прямой ссылке не принять: она не «чья-то», она адресована.
    [Fact]
    public async Task Accept_OfARequestAddressedToSomeoneElse_IsNotFound()
    {
        await using var factory = new PlatformApiFactory();
        var me = await SeedPersonAsync(factory, "1234");
        var friend = await SeedPersonAsync(factory, "4321");
        var stranger = await SeedPersonAsync(factory, "1111");

        using var myClient = factory.CreateClient();
        await AuthenticateAsync(myClient, me, "1234");
        await myClient.PostAsJsonAsync("/api/me/friends/requests", new SendFriendRequestRequest(friend.Phone));
        var mine = await myClient.GetFromJsonAsync<FriendsDto>("/api/me/friends");
        var outgoing = Assert.Single(mine!.Outgoing);

        using var strangerClient = factory.CreateClient();
        await AuthenticateAsync(strangerClient, stranger, "1111");
        var response = await strangerClient.PostAsync(
            $"/api/me/friends/requests/{outgoing.FriendRequestId:D}/accept", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PresenceSwitch_TurnsOffAndIsReportedBack()
    {
        await using var factory = new PlatformApiFactory();
        var me = await SeedPersonAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, me, "1234");

        var before = await client.GetFromJsonAsync<FriendsDto>("/api/me/friends");
        Assert.True(before!.ShowsPresence);

        var response = await client.PatchAsJsonAsync(
            "/api/me/friends/presence", new UpdatePresenceVisibilityRequest(false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = await response.Content.ReadFromJsonAsync<FriendsDto>();
        Assert.False(after!.ShowsPresence);
    }
}
