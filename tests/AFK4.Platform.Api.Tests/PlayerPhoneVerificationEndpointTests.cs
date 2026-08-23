using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
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
/// Подтверждение номера игроком. До него флаг `PhoneVerified` не выставлял никто, и обе
/// зависящие от него возможности — онлайн-пополнение и онлайн-бронь — были закрыты для всех.
/// </summary>
public sealed class PlayerPhoneVerificationEndpointTests
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

    private static async Task<SeededPlayer> SeedPlayerAsync(
        PlatformApiFactory factory, string pin, bool phoneVerified = false, Guid? organizationId = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var org = organizationId ?? Guid.NewGuid();
        var player = Guid.NewGuid();
        // Номер должен быть настоящим: нормализатор отвергает буквы, а `Guid.ToString("N")`
        // выдаёт шестнадцатеричные символы.
        var phone = TestPhones.Next();

        if (!await db.Organizations.AnyAsync(candidate => candidate.OrganizationId == org))
        {
            db.Organizations.Add(new OrganizationEntity
            {
                OrganizationId = org,
                Name = "Phone Test Org",
                CreatedAtUtc = Now
            });
        }

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

        // Клубная карточка остаётся только ради подтверждения номера: именно на ней живёт
        // `PhoneVerified`, который эти тесты и проверяют. Входом она больше не заведует — PIN
        // лежит на личности.
        db.PlayerCredentials.Add(new PlayerCredentialEntity
        {
            PlayerCredentialId = Guid.NewGuid(),
            PlayerAccountId = player,
            OrganizationId = org,
            PhoneVerified = phoneVerified,
            PhoneVerifiedAtUtc = phoneVerified ? Now : null,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        await PlayerPinTestData.AttachPersonWithPinAsync(
            factory, player, phone, pin, phoneVerified: phoneVerified);

        return new SeededPlayer(org, player, phone);
    }

    private static async Task AuthenticateAsync(HttpClient client, SeededPlayer player, string pin)
    {
        var signIn = await client.PostAsJsonAsync(
            "/api/public/player/sign-in",
            new PlayerSignInRequest(player.OrgId, player.Phone, pin));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    private static string CodeFrom(SmsMessage message) => message.Variables["code-1"];

    [Fact]
    public async Task StartThenConfirm_MarksThePhoneVerified()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<ISmsTransport>();
            services.AddSingleton<ISmsTransport>(recording);
        });
        using var client = factory.CreateClient();
        var player = await SeedPlayerAsync(factory, "1234");
        await AuthenticateAsync(client, player, "1234");

        var start = await client.PostAsJsonAsync(
            "/api/me/phone/start-verification",
            new PlayerPhoneStartVerificationRequest("+992 93 738-00-70"));
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        var sms = Assert.Single(recording.Sent);
        Assert.Equal("+992937380070", sms.ToPhoneNumber);

        var confirm = await client.PostAsJsonAsync(
            "/api/me/phone/confirm",
            new PlayerPhoneConfirmRequest(CodeFrom(sms)));
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var credential = db.PlayerCredentials.Single(c => c.PlayerAccountId == player.PlayerId);
        var account = db.PlayerAccounts.Single(a => a.PlayerAccountId == player.PlayerId);
        Assert.True(credential.PhoneVerified);
        Assert.NotNull(credential.PhoneVerifiedAtUtc);
        Assert.Equal("+992937380070", account.PhoneNumber);
    }

    // Подтверждение — это и есть ключ к онлайн-действиям: до него бронь отвечает 403.
    [Fact]
    public async Task Confirming_OpensTheBookingGate()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<ISmsTransport>();
            services.AddSingleton<ISmsTransport>(recording);
        });
        using var client = factory.CreateClient();
        var player = await SeedPlayerAsync(factory, "1234");
        await AuthenticateAsync(client, player, "1234");

        var beforeStatus = await client.GetAsync("/api/me/phone");
        var before = await beforeStatus.Content.ReadFromJsonAsync<PlayerPhoneStatusResponse>();
        Assert.Null(before!.PhoneVerifiedAtUtc);

        await client.PostAsJsonAsync(
            "/api/me/phone/start-verification",
            new PlayerPhoneStartVerificationRequest(player.Phone));
        var confirm = await client.PostAsJsonAsync(
            "/api/me/phone/confirm",
            new PlayerPhoneConfirmRequest(CodeFrom(Assert.Single(recording.Sent))));
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var afterStatus = await client.GetAsync("/api/me/phone");
        var after = await afterStatus.Content.ReadFromJsonAsync<PlayerPhoneStatusResponse>();
        Assert.NotNull(after!.PhoneVerifiedAtUtc);

        // Токен выдан до подтверждения, но право читается из базы на каждый запрос — заново
        // входить не нужно.
        var profile = await client.GetFromJsonAsync<PlayerProfileDto>("/api/me/profile");
        Assert.True(profile!.PhoneVerified);
    }

    [Fact]
    public async Task Confirm_WithWrongCode_ReportsRemainingAttempts()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<ISmsTransport>();
            services.AddSingleton<ISmsTransport>(recording);
        });
        using var client = factory.CreateClient();
        var player = await SeedPlayerAsync(factory, "1234");
        await AuthenticateAsync(client, player, "1234");

        await client.PostAsJsonAsync(
            "/api/me/phone/start-verification",
            new PlayerPhoneStartVerificationRequest(player.Phone));

        var confirm = await client.PostAsJsonAsync(
            "/api/me/phone/confirm",
            new PlayerPhoneConfirmRequest("000000"));

        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(db.PlayerCredentials.Single(c => c.PlayerAccountId == player.PlayerId).PhoneVerified);
    }

    // Второй код подряд — это либо нетерпеливый палец, либо перебор чужого номера.
    [Fact]
    public async Task StartTwice_IsRefusedUntilTheCooldownPasses()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<ISmsTransport>();
            services.AddSingleton<ISmsTransport>(recording);
        });
        using var client = factory.CreateClient();
        var player = await SeedPlayerAsync(factory, "1234");
        await AuthenticateAsync(client, player, "1234");

        var first = await client.PostAsJsonAsync(
            "/api/me/phone/start-verification",
            new PlayerPhoneStartVerificationRequest(player.Phone));
        var second = await client.PostAsJsonAsync(
            "/api/me/phone/start-verification",
            new PlayerPhoneStartVerificationRequest(player.Phone));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Single(recording.Sent);
    }

    [Fact]
    public async Task Start_WithNonsensePhone_IsRejectedBeforeAnySms()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<ISmsTransport>();
            services.AddSingleton<ISmsTransport>(recording);
        });
        using var client = factory.CreateClient();
        var player = await SeedPlayerAsync(factory, "1234");
        await AuthenticateAsync(client, player, "1234");

        var response = await client.PostAsJsonAsync(
            "/api/me/phone/start-verification",
            new PlayerPhoneStartVerificationRequest("не номер"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(recording.Sent);
    }

    // Номер опознаёт игрока внутри клуба: занятый в этом клубе номер занять нельзя, а тот же
    // номер в другом клубе — совершенно нормальный случай.
    [Fact]
    public async Task Confirm_WhenNumberBelongsToAnotherPlayerInTheSameClub_IsRefused()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<ISmsTransport>();
            services.AddSingleton<ISmsTransport>(recording);
        });
        using var client = factory.CreateClient();

        var owner = await SeedPlayerAsync(factory, "1234", phoneVerified: true);
        var newcomer = await SeedPlayerAsync(factory, "4321", organizationId: owner.OrgId);
        await AuthenticateAsync(client, newcomer, "4321");

        await client.PostAsJsonAsync(
            "/api/me/phone/start-verification",
            new PlayerPhoneStartVerificationRequest(owner.Phone));
        var confirm = await client.PostAsJsonAsync(
            "/api/me/phone/confirm",
            new PlayerPhoneConfirmRequest(CodeFrom(Assert.Single(recording.Sent))));

        Assert.Equal(HttpStatusCode.Conflict, confirm.StatusCode);
    }

    [Fact]
    public async Task PhoneEndpoints_WithoutBearer_AreUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var start = await client.PostAsJsonAsync(
            "/api/me/phone/start-verification",
            new PlayerPhoneStartVerificationRequest("+992937380070"));
        var status = await client.GetAsync("/api/me/phone");

        Assert.Equal(HttpStatusCode.Unauthorized, start.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, status.StatusCode);
    }
}
