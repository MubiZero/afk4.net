using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Вход по SMS-коду. Телефон и так опознаёт игрока внутри клуба, поэтому код доказывает то же,
/// что и PIN, — только его нечего забывать и незачем сбрасывать.
/// </summary>
public sealed class PlayerCodeSignInEndpointTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed class RecordingSmsTransport : ISmsTransport
    {
        public List<SmsMessage> Sent { get; } = [];

        public Task SendAsync(SmsMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed record SeededPlayer(Guid OrgId, Guid PlayerId, string Phone);

    private static async Task<SeededPlayer> SeedPlayerAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var org = Guid.NewGuid();
        var player = Guid.NewGuid();
        var phone = TestPhones.Next();

        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = org,
            Name = "Code Sign-In Org",
            CreatedAtUtc = Now
        });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = player,
            OrganizationId = org,
            HomeBranchId = Guid.NewGuid(),
            DisplayName = "Test Player",
            PhoneNumber = phone,
            PreferredLocale = "ru",
            IsActive = true,
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();

        return new SeededPlayer(org, player, phone);
    }

    private static PlatformApiFactory FactoryWith(RecordingSmsTransport recording) =>
        new(extraServices: services =>
        {
            services.RemoveAll<ISmsTransport>();
            services.AddSingleton<ISmsTransport>(recording);
        });

    private static string CodeFrom(SmsMessage message) => message.Variables["code-1"];

    // Игрок без пароля: код — единственный способ войти, и он должен работать.
    [Fact]
    public async Task CodeSignIn_IssuesTokens_WithoutAnyPassword()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = FactoryWith(recording);
        using var client = factory.CreateClient();
        var player = await SeedPlayerAsync(factory);

        var start = await client.PostAsJsonAsync(
            "/api/public/player/sign-in/code",
            new PlayerCodeSignInStartRequest(player.OrgId, player.Phone));
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        var confirm = await client.PostAsJsonAsync(
            "/api/public/player/sign-in/code/confirm",
            new PlayerCodeSignInRequest(player.OrgId, player.Phone, CodeFrom(Assert.Single(recording.Sent))));

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var tokens = await confirm.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        Assert.False(string.IsNullOrWhiteSpace(tokens!.AccessToken));
    }

    // Прочитать код с этого телефона — то же доказательство, которого требует подтверждение.
    [Fact]
    public async Task CodeSignIn_AlsoConfirmsThePhone()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = FactoryWith(recording);
        using var client = factory.CreateClient();
        var player = await SeedPlayerAsync(factory);

        await client.PostAsJsonAsync(
            "/api/public/player/sign-in/code",
            new PlayerCodeSignInStartRequest(player.OrgId, player.Phone));
        await client.PostAsJsonAsync(
            "/api/public/player/sign-in/code/confirm",
            new PlayerCodeSignInRequest(player.OrgId, player.Phone, CodeFrom(Assert.Single(recording.Sent))));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var credential = await db.PlayerCredentials.SingleAsync(c => c.PlayerAccountId == player.PlayerId);
        Assert.True(credential.PhoneVerified);
    }

    // Разный ответ на «свой» и «чужой» номер превратил бы эндпоинт в справочник, кто где играет.
    [Fact]
    public async Task Start_AnswersTheSameForUnknownNumbers_AndSendsNothing()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = FactoryWith(recording);
        using var client = factory.CreateClient();
        var player = await SeedPlayerAsync(factory);

        var known = await client.PostAsJsonAsync(
            "/api/public/player/sign-in/code",
            new PlayerCodeSignInStartRequest(player.OrgId, player.Phone));
        var unknown = await client.PostAsJsonAsync(
            "/api/public/player/sign-in/code",
            new PlayerCodeSignInStartRequest(player.OrgId, "+992900000001"));

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal(
            await known.Content.ReadAsStringAsync(),
            await unknown.Content.ReadAsStringAsync());
        Assert.Single(recording.Sent);
    }

    // Тот же номер в другом клубе — другой игрок, и код чужого клуба ему не подходит.
    [Fact]
    public async Task Confirm_InAnotherOrganization_IsRefused()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = FactoryWith(recording);
        using var client = factory.CreateClient();
        var player = await SeedPlayerAsync(factory);
        var otherClub = await SeedPlayerAsync(factory);

        await client.PostAsJsonAsync(
            "/api/public/player/sign-in/code",
            new PlayerCodeSignInStartRequest(player.OrgId, player.Phone));

        var confirm = await client.PostAsJsonAsync(
            "/api/public/player/sign-in/code/confirm",
            new PlayerCodeSignInRequest(otherClub.OrgId, player.Phone, CodeFrom(Assert.Single(recording.Sent))));

        Assert.Equal(HttpStatusCode.Gone, confirm.StatusCode);
    }

    [Fact]
    public async Task Confirm_WithWrongCode_DoesNotSignIn()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = FactoryWith(recording);
        using var client = factory.CreateClient();
        var player = await SeedPlayerAsync(factory);

        await client.PostAsJsonAsync(
            "/api/public/player/sign-in/code",
            new PlayerCodeSignInStartRequest(player.OrgId, player.Phone));
        var confirm = await client.PostAsJsonAsync(
            "/api/public/player/sign-in/code/confirm",
            new PlayerCodeSignInRequest(player.OrgId, player.Phone, "000000"));

        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
    }

    [Fact]
    public async Task Confirm_WithoutAnyCode_SaysThereIsNone()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var player = await SeedPlayerAsync(factory);

        var confirm = await client.PostAsJsonAsync(
            "/api/public/player/sign-in/code/confirm",
            new PlayerCodeSignInRequest(player.OrgId, player.Phone, "123456"));

        Assert.Equal(HttpStatusCode.Gone, confirm.StatusCode);
    }

    // Один код на руках — второй не отправляется, но ответ остаётся тем же.
    [Fact]
    public async Task Start_Twice_SendsOnlyOneSms_ButAnswersTheSame()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = FactoryWith(recording);
        using var client = factory.CreateClient();
        var player = await SeedPlayerAsync(factory);

        var first = await client.PostAsJsonAsync(
            "/api/public/player/sign-in/code",
            new PlayerCodeSignInStartRequest(player.OrgId, player.Phone));
        var second = await client.PostAsJsonAsync(
            "/api/public/player/sign-in/code",
            new PlayerCodeSignInStartRequest(player.OrgId, player.Phone));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Single(recording.Sent);
    }

    [Fact]
    public async Task Start_WithNonsensePhone_IsRejected()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = FactoryWith(recording);
        using var client = factory.CreateClient();
        var player = await SeedPlayerAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/public/player/sign-in/code",
            new PlayerCodeSignInStartRequest(player.OrgId, "не номер"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(recording.Sent);
    }
}
