using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Identity;

/// <summary>
/// PIN задаёт сам человек и только в приложении: он уже вошёл, и это не стоит ни одной SMS.
/// Забывшему PIN здесь же выдаётся новый — без кода, без старого PIN и без администратора.
/// Спросить старое значило бы запереть выход ровно тому, кто за ним пришёл.
/// </summary>
public sealed class MePinEndpointTests
{
    [Fact]
    public async Task SetPin_StoresHash_AndMeReportsPinSet()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000501");
        var club = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId);

        using var client = factory.CreateClient();
        await AuthorizeAsync(factory, client, person.PlatformPersonId, club.PlayerAccountId);

        var response = await client.PutAsJsonAsync("/api/me/pin", new SetMyPinRequest("1234"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var stored = await ReadPersonAsync(factory, person.PlatformPersonId);
        Assert.NotNull(stored.PinHash);
        Assert.NotEqual("1234", stored.PinHash);
        Assert.NotNull(stored.PinSetAtUtc);

        var me = await client.GetFromJsonAsync<MeDto>("/api/me");
        Assert.True(me!.Person.PinSet);
    }

    // Сервер PIN не отдаёт никогда: наружу выходит только признак «задан или нет».
    [Fact]
    public async Task Me_NeverReturnsThePinItself()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000502");
        var club = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId);

        using var client = factory.CreateClient();
        await AuthorizeAsync(factory, client, person.PlatformPersonId, club.PlayerAccountId);
        await client.PutAsJsonAsync("/api/me/pin", new SetMyPinRequest("4321"));

        var body = await client.GetStringAsync("/api/me");

        Assert.Contains("\"pinSet\":true", body);
        Assert.DoesNotContain("4321", body);
        Assert.DoesNotContain("pinHash", body, StringComparison.OrdinalIgnoreCase);
    }

    // Забыл PIN — задаёт новый там же. Ни старого PIN, ни кода из SMS: человек уже доказал, что он
    // это он, когда входил в приложение.
    [Fact]
    public async Task ChangePin_AsksForNothingButTheAppToken()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000503");
        var club = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId);

        using var client = factory.CreateClient();
        await AuthorizeAsync(factory, client, person.PlatformPersonId, club.PlayerAccountId);

        await client.PutAsJsonAsync("/api/me/pin", new SetMyPinRequest("1234"));
        var firstHash = (await ReadPersonAsync(factory, person.PlatformPersonId)).PinHash;

        var changed = await client.PutAsJsonAsync("/api/me/pin", new SetMyPinRequest("567890"));

        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.NotEqual(firstHash, (await ReadPersonAsync(factory, person.PlatformPersonId)).PinHash);
    }

    // Пять неверных попыток у ПК запирают самопосадку на 15 минут. Человек, который для этого и
    // пришёл в приложение, обязан выйти из блокировки сам — иначе она бьёт по жертве, а не по
    // подбирающему.
    [Fact]
    public async Task SetPin_ClearsTheLockout()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000504");
        var club = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId);
        await LockOutAsync(factory, person.PlatformPersonId);

        using var client = factory.CreateClient();
        await AuthorizeAsync(factory, client, person.PlatformPersonId, club.PlayerAccountId);

        await client.PutAsJsonAsync("/api/me/pin", new SetMyPinRequest("1234"));

        var stored = await ReadPersonAsync(factory, person.PlatformPersonId);
        Assert.Equal(0, stored.PinFailedCount);
        Assert.Null(stored.PinLockedUntilUtc);
    }

    // PIN принадлежит личности, а не клубному счёту. Человек, зарегистрировавшийся дома, задаёт его
    // до первого визита — и маршрут не имеет права требовать от него выбрать клуб.
    [Fact]
    public async Task SetPin_WorksForAPersonWithoutASingleClub()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000505");

        using var client = factory.CreateClient();
        await AuthorizeAsync(factory, client, person.PlatformPersonId, playerAccountId: null);

        var response = await client.PutAsJsonAsync("/api/me/pin", new SetMyPinRequest("1234"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.NotNull((await ReadPersonAsync(factory, person.PlatformPersonId)).PinHash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("123456789")]
    [InlineData("12a4")]
    [InlineData("12 34")]
    [InlineData("пароль")]
    public async Task SetPin_RefusesAnythingButFourToEightDigits(string pin)
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000506");
        var club = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId);

        using var client = factory.CreateClient();
        await AuthorizeAsync(factory, client, person.PlatformPersonId, club.PlayerAccountId);

        var response = await client.PutAsJsonAsync("/api/me/pin", new SetMyPinRequest(pin));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_pin", await response.Content.ReadAsStringAsync());
        Assert.Null((await ReadPersonAsync(factory, person.PlatformPersonId)).PinHash);
    }

    [Fact]
    public async Task SetPin_WithoutAToken_Is401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/me/pin", new SetMyPinRequest("1234"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task AuthorizeAsync(
        PlatformApiFactory factory, HttpClient client, Guid platformPersonId, Guid? playerAccountId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<IPlatformPersonTokenService>();
        var person = await db.PlatformPersons.SingleAsync(
            candidate => candidate.PlatformPersonId == platformPersonId);
        var account = playerAccountId is { } id
            ? await db.PlayerAccounts.SingleAsync(candidate => candidate.PlayerAccountId == id)
            : null;
        var session = await tokens.IssueAsync(person, account, CancellationToken.None);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);
    }

    private static async Task<PlatformPersonEntity> ReadPersonAsync(
        PlatformApiFactory factory, Guid platformPersonId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.PlatformPersons.AsNoTracking().SingleAsync(
            candidate => candidate.PlatformPersonId == platformPersonId);
    }

    private static async Task LockOutAsync(PlatformApiFactory factory, Guid platformPersonId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var person = await db.PlatformPersons.SingleAsync(
            candidate => candidate.PlatformPersonId == platformPersonId);
        person.PinFailedCount = 5;
        person.PinLockedUntilUtc = DateTimeOffset.UtcNow.AddMinutes(15);
        await db.SaveChangesAsync();
    }
}
