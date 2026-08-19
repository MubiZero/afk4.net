using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Identity;

/// <summary>
/// Какой клуб человек имеет в виду. Токен теперь принадлежит человеку, поэтому клуб приходится
/// выбирать на каждом запросе, и порядок выбора — это вся совместимость со старыми клиентами.
/// Ошибка здесь показывает игроку чужой кошелёк или разлогинивает всех разом, поэтому проверяются
/// все четыре ветки, а не «основная».
/// </summary>
public sealed class PlayerAuthenticationContextTests
{
    [Fact]
    public async Task RequestedClubWins_EvenWhenTheTokenRemembersAnother()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000201");
        var pinned = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId, "Клуб входа");
        var other = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId, "Соседний клуб");

        using var client = factory.CreateClient();
        Authorize(client, await IssueAsync(factory, person.PlatformPersonId, pinned.PlayerAccountId));
        client.DefaultRequestHeaders.Add(
            PlayerAuthenticationMiddleware.OrganizationHeader, other.OrganizationId.ToString());

        var profile = await client.GetFromJsonAsync<PlayerProfileDto>("/api/me/profile");
        Assert.Equal(other.PlayerAccountId, profile!.PlayerAccountId);
    }

    [Fact]
    public async Task WithoutAHeader_TheClubChosenAtSignInIsUsed()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000202");
        var pinned = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId, "Клуб входа");
        await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId, "Соседний клуб");

        using var client = factory.CreateClient();
        Authorize(client, await IssueAsync(factory, person.PlatformPersonId, pinned.PlayerAccountId));

        // Старый клиент про заголовок не знает и обязан попасть туда же, куда попадал вчера.
        var profile = await client.GetFromJsonAsync<PlayerProfileDto>("/api/me/profile");
        Assert.Equal(pinned.PlayerAccountId, profile!.PlayerAccountId);
    }

    [Fact]
    public async Task WithNothingNamed_ASingleClubNeedsNoChoosing()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000203");
        var only = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId);

        using var client = factory.CreateClient();
        Authorize(client, await IssueUnpinnedTokenAsync(factory, person.PlatformPersonId));

        var profile = await client.GetFromJsonAsync<PlayerProfileDto>("/api/me/profile");
        Assert.Equal(only.PlayerAccountId, profile!.PlayerAccountId);
    }

    [Fact]
    public async Task WithSeveralClubsAndNothingNamed_TheAnswerIsWhichClub_NotWhoAreYou()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000204");
        await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId, "Первый");
        await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId, "Второй");

        using var client = factory.CreateClient();
        Authorize(client, await IssueUnpinnedTokenAsync(factory, person.PlatformPersonId));

        var response = await client.GetAsync("/api/me/profile");

        // 401 отправил бы приложение на повторный вход, из которого оно вернулось бы с тем же.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("club_not_selected", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ClubNamedButNotHis_IsRefused_NotSilentlySwappedForAnother()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000205");
        var his = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId);
        var stranger = await PlatformPersonTestData.AddClubAsync(factory, Guid.NewGuid(), "Чужой клуб");

        using var client = factory.CreateClient();
        Authorize(client, await IssueAsync(factory, person.PlatformPersonId, his.PlayerAccountId));
        client.DefaultRequestHeaders.Add(
            PlayerAuthenticationMiddleware.OrganizationHeader, stranger.OrganizationId.ToString());

        var response = await client.GetAsync("/api/me/profile");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UnreadableClubHeader_IsARefusal_NotAFallback()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000206");
        var pinned = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId);

        using var client = factory.CreateClient();
        Authorize(client, await IssueAsync(factory, person.PlatformPersonId, pinned.PlayerAccountId));
        client.DefaultRequestHeaders.Add(PlayerAuthenticationMiddleware.OrganizationHeader, "не-клуб");

        var response = await client.GetAsync("/api/me/profile");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_organization", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task NoTokenAtAll_IsStillUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    private static void Authorize(HttpClient client, PlatformPersonSessionResponse tokens) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

    private static async Task<PlatformPersonSessionResponse> IssueAsync(
        PlatformApiFactory factory, Guid platformPersonId, Guid playerAccountId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IPlatformPersonTokenService>();
        var person = await db.PlatformPersons.SingleAsync(
            candidate => candidate.PlatformPersonId == platformPersonId);
        var account = await db.PlayerAccounts.SingleAsync(
            candidate => candidate.PlayerAccountId == playerAccountId);
        return await service.IssueAsync(person, account, CancellationToken.None);
    }

    /// <summary>
    /// Токен без закреплённого клуба. Такие выдаёт самостоятельная регистрация: человек скачал
    /// приложение дома и ни в один клуб ещё не заходил.
    /// </summary>
    private static async Task<PlatformPersonSessionResponse> IssueUnpinnedTokenAsync(
        PlatformApiFactory factory, Guid platformPersonId)
    {
        var tokenId = Guid.NewGuid();
        var token = $"{tokenId:N}.{Convert.ToHexString(RandomNumberGenerator.GetBytes(32))}";

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.PlatformPersonAccessTokens.Add(new PlatformPersonAccessTokenEntity
        {
            PlatformPersonAccessTokenId = tokenId,
            PlatformPersonId = platformPersonId,
            PinnedOrganizationId = null,
            TokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token)),
            CreatedAtUtc = PlatformPersonTestData.Now,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
        });
        await db.SaveChangesAsync();

        return new PlatformPersonSessionResponse(
            null, null, "Фаррух", true,
            token, DateTimeOffset.UtcNow.AddHours(1), "unused", DateTimeOffset.UtcNow.AddDays(30),
            platformPersonId, "tg", ProfileCompleted: true);
    }
}
