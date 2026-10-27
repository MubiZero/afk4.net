using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Tests.Devices;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Games;
using AFK4.Shared.Contracts.Install;
using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Games;

/// <summary>
/// Кеш игр клуба (docs/operations/game-cache-lancache.md): по умолчанию встроенный в агента ПК администратора —
/// клубу ничего не настраивать; можно указать свой сервер LANCache или выключить. Агенты получают адрес
/// сердцебиением и сообщают, видят ли кеш, — Панель показывает, со скольких ПК.
/// </summary>
public sealed class GameCacheEndpointTests
{
    private static readonly string Route = $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/game-cache";

    [Fact]
    public async Task TheCacheAddress_ReachesThePcs_AndThePanelSeesWhoReachesTheCache()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var enrollment = await TestDeviceEnrollment.EnrollDeviceAsync(client, TestIds.OrganizationId, TestIds.BranchId);

        Assert.Equal(string.Empty, (await HeartbeatAsync(client, enrollment, reachable: null))!.GameCacheAddress);

        var saved = await client.PutAsJsonAsync(Route, new UpdateBranchGameCacheRequest("192.168.1.50"));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("192.168.1.50", (await HeartbeatAsync(client, enrollment, reachable: true))!.GameCacheAddress);

        var status = await client.GetFromJsonAsync<BranchGameCacheDto>(Route);
        Assert.Equal(new BranchGameCacheDto("192.168.1.50", 1, 1, EffectiveAddress: "192.168.1.50"), status);

        // Новый адрес — прежние отметки «видит» были о другом сервере.
        await client.PutAsJsonAsync(Route, new UpdateBranchGameCacheRequest("192.168.1.60"));
        Assert.Equal(new BranchGameCacheDto("192.168.1.60", 0, 0, EffectiveAddress: "192.168.1.60"), await client.GetFromJsonAsync<BranchGameCacheDto>(Route));

        await client.PutAsJsonAsync(Route, new UpdateBranchGameCacheRequest(null));
        Assert.Equal(string.Empty, (await HeartbeatAsync(client, enrollment, reachable: null))!.GameCacheAddress);
    }

    // Без всякой настройки: ПК администратора поднял встроенный кеш — игровые ПК идут к нему, а сам он
    // к себе не направляется. Клуб выключил кеш — все качают напрямую, ПК администратора кеш гасит.
    [Fact]
    public async Task ByDefault_TheAdminPcIsTheCache_WithoutAnySetup()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var admin = await TestDeviceEnrollment.EnrollDeviceAsync(client, TestIds.OrganizationId, TestIds.BranchId, machineName: "ADMIN-PC", publicKey: "admin-key");
        var gaming = await TestDeviceEnrollment.EnrollDeviceAsync(client, TestIds.OrganizationId, TestIds.BranchId, machineName: "PC-002", publicKey: "gaming-key");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            (await db.Devices.SingleAsync(device => device.DeviceId == admin.DeviceId)).Role = DeviceRoleNames.ManagerWorkstation;
            await db.SaveChangesAsync();
        }

        var adminBeat = await HeartbeatAsync(client, admin, reachable: null, serving: true, address: "192.168.1.5");
        Assert.True(adminBeat!.GameCacheHost);
        Assert.Equal(string.Empty, adminBeat.GameCacheAddress);
        Assert.Equal("192.168.1.5", (await HeartbeatAsync(client, gaming, reachable: true))!.GameCacheAddress);
        var status = await client.GetFromJsonAsync<BranchGameCacheDto>(Route);
        Assert.Equal(("192.168.1.5", 1, true), (status!.EffectiveAddress, status.DevicesSeeing, status.Enabled));

        await client.PutAsJsonAsync(Route, new UpdateBranchGameCacheRequest(null, Enabled: false));
        Assert.False((await HeartbeatAsync(client, admin, reachable: null, serving: true, address: "192.168.1.5"))!.GameCacheHost);
        Assert.Equal(string.Empty, (await HeartbeatAsync(client, gaming, reachable: null))!.GameCacheAddress);
    }

    // Адрес из интернета увёл бы загрузки всех ПК клуба на чужой сервер.
    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("cache.example.com")]
    public async Task OnlyAClubNetworkAddressIsAccepted(string address)
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(Route, new UpdateBranchGameCacheRequest(address))).StatusCode);
    }

    [Fact]
    public async Task SomeoneWhoDoesNotManageGames_CannotSetTheCache()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Accountant);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(Route, new UpdateBranchGameCacheRequest("192.168.1.50"))).StatusCode);
    }

    private static async Task<DeviceHeartbeatResponse?> HeartbeatAsync(
        HttpClient client, InstallEnrollResponse enrollment, bool? reachable, bool? serving = null, string? address = null)
    {
        var request = new DeviceHeartbeatRequest(
            TestIds.OrganizationId, TestIds.BranchId, enrollment.DeviceId, "PC-001", "0.1.0", "0.1.0",
            DateTimeOffset.UtcNow, IsLocked: true, ActiveSessionId: null, ActiveSessionLeaseExpiresAtUtc: null, ActiveSessionLeaseSequence: null,
            NetworkMacAddress: address is null ? null : "AA-BB-CC-DD-EE-FF", NetworkAddress: address,
            GameCacheReachable: reachable, GameCacheServing: serving, GameCacheStoredBytes: serving == true ? 1024 : null);
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/devices/{enrollment.DeviceId}/heartbeat")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, enrollment.CredentialSecret);
        using var response = await client.SendAsync(message);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<DeviceHeartbeatResponse>();
    }
}
