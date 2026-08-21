using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AFK4.Platform.Api.Tests.Identity;

/// <summary>
/// Приложение игрока и веб-версия не правятся в этой волне, поэтому переход на личность обязан
/// быть для них незаметным. Тест ходит ровно теми же запросами, что и настоящий клиент: вход по
/// коду с организацией в теле, дальше только заголовок Authorization, никакого выбора клуба, и
/// продление по тому же полю refreshToken.
/// </summary>
public sealed class PlayerClientCompatibilityTests
{
    private sealed class RecordingSmsTransport : ISmsTransport
    {
        public List<SmsMessage> Sent { get; } = [];

        public Task SendAsync(SmsMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task GluedPlayer_SignsInAndBrowsesExactlyAsBefore()
    {
        var sms = new RecordingSmsTransport();
        await using var factory = FactoryWith(sms);
        var person = await PlatformPersonTestData.AddPersonAsync(
            factory, "+992900000301", phoneVerified: false);
        var account = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId);
        await SetAccountPhoneAsync(factory, account.PlayerAccountId, person.PhoneNumber);

        using var client = factory.CreateClient();
        var tokens = await SignInLikeTheAppDoesAsync(client, sms, account.OrganizationId, person.PhoneNumber);

        // Клиент читает ровно те же поля, что и до перехода.
        Assert.Equal(account.PlayerAccountId, tokens.PlayerAccountId);
        Assert.Equal(account.OrganizationId, tokens.OrganizationId);
        Assert.True(tokens.PhoneVerified);

        // Вход действительно пошёл по новой дороге: токен выдан личности, а не клубному счёту.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            Assert.True(await db.PlatformPersonAccessTokens.AnyAsync(
                token => token.PlatformPersonId == person.PlatformPersonId
                    && token.PinnedOrganizationId == account.OrganizationId));
            Assert.False(await db.PlayerAccessTokens.AnyAsync());
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var profile = await client.GetAsync("/api/me/profile");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Equal(
            account.PlayerAccountId,
            (await profile.Content.ReadFromJsonAsync<PlayerProfileDto>())!.PlayerAccountId);

        // Ни один клиентский маршрут не должен отвечать «в каком клубе» — клуб уже закреплён.
        foreach (var path in new[] { "/api/me/dashboard", "/api/me/wallet", "/api/me/reservations" })
        {
            var response = await client.GetAsync(path);
            Assert.NotEqual(HttpStatusCode.Conflict, response.StatusCode);
            Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // Продление — тем же полем и с тем же ответом; клуб за токеном сохраняется.
        var refreshed = await client.PostAsJsonAsync(
            "/api/public/player/refresh", new PlayerRefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var renewed = await refreshed.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        Assert.Equal(account.PlayerAccountId, renewed!.PlayerAccountId);
        Assert.Equal(account.OrganizationId, renewed.OrganizationId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", renewed.AccessToken);
        Assert.Equal(
            account.PlayerAccountId,
            (await client.GetFromJsonAsync<PlayerProfileDto>("/api/me/profile"))!.PlayerAccountId);
    }

    [Fact]
    public async Task AccountNotYetGluedToAPerson_StillSignsInAndWorks()
    {
        var sms = new RecordingSmsTransport();
        await using var factory = FactoryWith(sms);
        // Дубль внутри клуба после переноса: личности у счёта нет, и вход обязан остаться.
        var account = await PlatformPersonTestData.AddClubAsync(factory, platformPersonId: null);
        await SetAccountPhoneAsync(factory, account.PlayerAccountId, "+992900000302");

        using var client = factory.CreateClient();
        var tokens = await SignInLikeTheAppDoesAsync(client, sms, account.OrganizationId, "+992900000302");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var profile = await client.GetFromJsonAsync<PlayerProfileDto>("/api/me/profile");
        Assert.Equal(account.PlayerAccountId, profile!.PlayerAccountId);
    }

    private static async Task<PlayerSignInResponse> SignInLikeTheAppDoesAsync(
        HttpClient client, RecordingSmsTransport sms, Guid organizationId, string phone)
    {
        var start = await client.PostAsJsonAsync(
            "/api/public/player/sign-in/code", new PlayerCodeSignInStartRequest(organizationId, phone));
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        var code = sms.Sent[^1].Variables["code-1"];
        var confirm = await client.PostAsJsonAsync(
            "/api/public/player/sign-in/code/confirm",
            new PlayerCodeSignInRequest(organizationId, phone, code));
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        return (await confirm.Content.ReadFromJsonAsync<PlayerSignInResponse>())!;
    }

    private static async Task SetAccountPhoneAsync(PlatformApiFactory factory, Guid playerAccountId, string phone)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var account = await db.PlayerAccounts.SingleAsync(
            candidate => candidate.PlayerAccountId == playerAccountId);
        account.PhoneNumber = phone;
        await db.SaveChangesAsync();
    }

    private static PlatformApiFactory FactoryWith(RecordingSmsTransport sms) =>
        new(extraServices: services =>
        {
            services.RemoveAll<ISmsTransport>();
            services.AddSingleton<ISmsTransport>(sms);
        });
}
