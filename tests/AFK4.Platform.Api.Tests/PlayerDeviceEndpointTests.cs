using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Notifications;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

/// Телефон игрока как адрес доставки: приложение сообщает токен при входе и снимает при выходе.
public sealed class PlayerDeviceEndpointTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed record PlayerContext(Guid OrgId, Guid PlayerId, string Phone);

    private static async Task<PlayerContext> SeedPlayerAsync(PlatformApiFactory factory, PlayerContext? sameClubAs = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var orgId = sameClubAs?.OrgId ?? Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        // Номер должен быть из одних цифр: вход нормализует его до E.164, и шестнадцатеричные
        // буквы из Guid оставили бы меньше одиннадцати цифр — такой номер отвергается.
        var phone = $"+99290003{(uint)playerId.GetHashCode() % 10_000:D4}";

        if (sameClubAs is null)
        {
            db.Organizations.Add(new OrganizationEntity
            {
                OrganizationId = orgId,
                Slug = $"club-{orgId.ToString("N")[..6]}",
                Name = "CyberX",
                Status = "active",
                CreatedAtUtc = Now,
                UpdatedAtUtc = Now
            });
        }

        db.Branches.Add(new BranchEntity
        {
            BranchId = branchId,
            OrganizationId = orgId,
            Slug = $"branch-{branchId.ToString("N")[..6]}",
            Name = "На Рудаки",
            City = "Душанбе",
            CreatedAtUtc = Now
        });

        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerId,
            OrganizationId = orgId,
            HomeBranchId = branchId,
            DisplayName = "Иван",
            PhoneNumber = phone,
            IsActive = true,
            CreatedAtUtc = Now
        });

        await db.SaveChangesAsync();
        await PlayerPinTestData.AttachPersonWithPinAsync(factory, playerId, phone, "1234");
        return new PlayerContext(orgId, playerId, phone);
    }

    private static async Task AuthenticateAsync(HttpClient client, PlayerContext context)
    {
        var signIn = await client.PostAsJsonAsync(
            "/api/public/player/sign-in", new PlayerSignInRequest(context.OrgId, context.Phone, "1234"));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    private static async Task<List<PlayerDeviceEntity>> DevicesAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.PlayerDevices.AsNoTracking().ToListAsync();
    }

    [Fact]
    public async Task Register_StoresTheDeviceForThePlayer()
    {
        await using var factory = new PlatformApiFactory();
        var context = await SeedPlayerAsync(factory);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, context);

        var response = await client.PostAsJsonAsync(
            "/api/me/devices", new RegisterPlayerDeviceRequest("token-1", "android", "ru"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var device = Assert.Single(await DevicesAsync(factory));
        Assert.Equal(context.PlayerId, device.PlayerAccountId);
        Assert.Equal("token-1", device.PushToken);
        Assert.Equal("android", device.Platform);
        Assert.Equal("ru", device.Locale);
    }

    /// Приложение подтверждает токен при каждом входе — это не повод плодить строки.
    [Fact]
    public async Task Register_SameTokenTwice_KeepsOneDevice()
    {
        await using var factory = new PlatformApiFactory();
        var context = await SeedPlayerAsync(factory);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, context);

        await client.PostAsJsonAsync("/api/me/devices", new RegisterPlayerDeviceRequest("token-1", "android", "ru"));
        await client.PostAsJsonAsync("/api/me/devices", new RegisterPlayerDeviceRequest("token-1", "android", "en"));

        var device = Assert.Single(await DevicesAsync(factory));
        Assert.Equal("en", device.Locale);
    }

    /// Один телефон, двое игроков: уведомления про сессию нового не должны идти прежнему.
    [Fact]
    public async Task Register_SameTokenAfterAnotherPlayerSignsIn_MovesTheDevice()
    {
        await using var factory = new PlatformApiFactory();
        var first = await SeedPlayerAsync(factory);
        var second = await SeedPlayerAsync(factory, sameClubAs: first);

        using (var client = factory.CreateClient())
        {
            await AuthenticateAsync(client, first);
            await client.PostAsJsonAsync("/api/me/devices", new RegisterPlayerDeviceRequest("shared-phone", "android", "ru"));
        }

        using (var client = factory.CreateClient())
        {
            await AuthenticateAsync(client, second);
            await client.PostAsJsonAsync("/api/me/devices", new RegisterPlayerDeviceRequest("shared-phone", "android", "ru"));
        }

        var device = Assert.Single(await DevicesAsync(factory));
        Assert.Equal(second.PlayerId, device.PlayerAccountId);
    }

    [Fact]
    public async Task Register_UnknownPlatform_ReturnsBadRequest()
    {
        await using var factory = new PlatformApiFactory();
        var context = await SeedPlayerAsync(factory);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, context);

        var response = await client.PostAsJsonAsync(
            "/api/me/devices", new RegisterPlayerDeviceRequest("token-1", "symbian", "ru"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await DevicesAsync(factory));
    }

    [Fact]
    public async Task Register_EmptyToken_ReturnsBadRequest()
    {
        await using var factory = new PlatformApiFactory();
        var context = await SeedPlayerAsync(factory);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, context);

        var response = await client.PostAsJsonAsync(
            "/api/me/devices", new RegisterPlayerDeviceRequest("   ", "android", "ru"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithoutToken_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        await SeedPlayerAsync(factory);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/me/devices", new RegisterPlayerDeviceRequest("token-1", "android", "ru"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesTheDevice()
    {
        await using var factory = new PlatformApiFactory();
        var context = await SeedPlayerAsync(factory);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, context);
        await client.PostAsJsonAsync("/api/me/devices", new RegisterPlayerDeviceRequest("token-1", "android", "ru"));

        var response = await client.DeleteAsync("/api/me/devices/token-1");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await DevicesAsync(factory));
    }

    /// Чужое устройство снять нельзя — иначе игрок отключил бы уведомления соседу.
    [Fact]
    public async Task Delete_AnotherPlayersDevice_LeavesItAlone()
    {
        await using var factory = new PlatformApiFactory();
        var owner = await SeedPlayerAsync(factory);
        var stranger = await SeedPlayerAsync(factory, sameClubAs: owner);

        using (var client = factory.CreateClient())
        {
            await AuthenticateAsync(client, owner);
            await client.PostAsJsonAsync("/api/me/devices", new RegisterPlayerDeviceRequest("owner-token", "android", "ru"));
        }

        using (var client = factory.CreateClient())
        {
            await AuthenticateAsync(client, stranger);
            var response = await client.DeleteAsync("/api/me/devices/owner-token");
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var device = Assert.Single(await DevicesAsync(factory));
        Assert.Equal(owner.PlayerId, device.PlayerAccountId);
    }
}
