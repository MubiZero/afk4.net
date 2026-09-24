using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Devices;

/// <summary>
/// Обслуживание помнит сервер, а не только агент. Пока правда жила на одной машине, стойка видела
/// запертый ПК свободным, начинала на нём сессию, и разблокировка молча снимала обслуживание.
/// </summary>
public sealed class DeviceMaintenanceEndpointTests
{
    [Fact]
    public async Task APcUnderMaintenance_CannotStartASession_UntilItIsBack()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        var seatId = await SeatIdAsync(fixture);

        Assert.Equal(HttpStatusCode.OK, (await fixture.CommandAsync(DeviceCommandTypeNames.MaintenanceOn)).StatusCode);
        var refused = await StartAsync(fixture, seatId, "start-1");

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("device_in_maintenance", await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.OK, (await fixture.CommandAsync(DeviceCommandTypeNames.MaintenanceOff)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await StartAsync(fixture, seatId, "start-2")).StatusCode);
    }

    [Fact]
    public async Task TheFloorMap_ShowsMaintenance_EvenForAPcThatIsOnline()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        await fixture.HeartbeatAsync();

        await fixture.CommandAsync(DeviceCommandTypeNames.MaintenanceOn);
        Assert.Equal(SeatStateNames.Maintenance, (await SeatAsync(fixture)).State);

        await fixture.CommandAsync(DeviceCommandTypeNames.MaintenanceOff);
        Assert.NotEqual(SeatStateNames.Maintenance, (await SeatAsync(fixture)).State);
    }

    [Fact]
    public async Task TheHeartbeat_TellsTheAgent_AndHidesTheSeatingCode()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();

        await fixture.CommandAsync(DeviceCommandTypeNames.MaintenanceOn);
        var closed = await fixture.HeartbeatAsync();

        // Агент, пропустивший команду, догоняет по этому признаку; код посадки звал бы к закрытому ПК.
        Assert.True(closed.Maintenance);
        Assert.Null(closed.SeatingCode);

        await fixture.CommandAsync(DeviceCommandTypeNames.MaintenanceOff);
        var open = await fixture.HeartbeatAsync();

        Assert.False(open.Maintenance);
        Assert.NotNull(open.SeatingCode);
    }

    [Fact]
    public async Task RepeatingMaintenanceOn_KeepsWhenItStarted()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();

        await fixture.CommandAsync(DeviceCommandTypeNames.MaintenanceOn);
        fixture.Clock.Advance(TimeSpan.FromMinutes(20));
        await fixture.CommandAsync(DeviceCommandTypeNames.MaintenanceOn);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var device = await db.Devices.SingleAsync(candidate => candidate.DeviceId == fixture.Device.DeviceId);
        Assert.Equal(DevicePlayerFixture.Start, device.MaintenanceSinceUtc);
    }

    [Fact]
    public async Task SigningThePlayerOut_RevokesTheirTokensOnTheServer()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        Assert.Equal(HttpStatusCode.OK, (await fixture.SignInAsync(fixture.Pin)).StatusCode);
        Assert.NotEmpty(await fixture.LiveTokensAsync());

        Assert.Equal(HttpStatusCode.OK, (await fixture.CommandAsync(DeviceCommandTypeNames.SignOut)).StatusCode);

        // Хост мог команду не получить; погасшие токены аккаунт всё равно не откроют.
        Assert.Empty(await fixture.LiveTokensAsync());
    }

    [Fact]
    public async Task SigningIn_OnAPcUnderMaintenance_IsRefused_BeforeThePinIsChecked()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        await fixture.CommandAsync(DeviceCommandTypeNames.MaintenanceOn);

        var refused = await fixture.SignInAsync(fixture.Pin);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(
            DevicePlayerSignInErrorCodeNames.DeviceInMaintenance,
            (await refused.Content.ReadFromJsonAsync<DevicePlayerSignInErrorDto>())!.Error);
        Assert.Empty(await fixture.LiveTokensAsync());

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        // Закрытый ПК не тратит попытки входа: после обслуживания счёт не должен стоять у порога.
        Assert.Equal(0, (await db.Devices.SingleAsync(device => device.DeviceId == fixture.Device.DeviceId)).PlayerSignInFailedCount);
    }

    [Fact]
    public async Task AQrClaimMadeBeforeMaintenance_DoesNotOpenThePc()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        using var phone = await fixture.PhoneClientAsync();
        var code = (await fixture.HeartbeatAsync()).SeatingCode!;
        var created = await phone.PostAsJsonAsync(
            "/api/me/devices/sign-in-claims",
            new CreatePlayerSignInClaimRequest(code, Guid.NewGuid().ToString("N")));
        var claim = (await created.Content.ReadFromJsonAsync<PlayerSignInClaimDto>())!;

        await fixture.CommandAsync(DeviceCommandTypeNames.MaintenanceOn);
        using var message = new HttpRequestMessage(
            HttpMethod.Post, $"/api/devices/{fixture.Device.DeviceId}/sign-in-claims/{claim.ClaimId}/redeem")
        {
            Content = JsonContent.Create(new DeviceRedeemSignInClaimRequest(
                fixture.Device.OrganizationId, fixture.Device.BranchId, fixture.Device.DeviceId))
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, fixture.Device.CredentialSecret);
        var redeemed = await fixture.Client.SendAsync(message);

        Assert.Equal(HttpStatusCode.Conflict, redeemed.StatusCode);
        Assert.Contains(PlayerSignInClaimErrorCodeNames.DeviceInMaintenance, await redeemed.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Empty(await fixture.LiveTokensAsync());
    }

    private static Task<HttpResponseMessage> StartAsync(DevicePlayerFixture fixture, Guid seatId, string idempotencyKey) =>
        fixture.Client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/sessions/start",
            new StartGuestSessionRequest(TestIds.OrganizationId, seatId, "manual-v1", idempotencyKey, SessionDurationModes.Fixed, 60));

    private static async Task<SeatStatusDto> SeatAsync(DevicePlayerFixture fixture)
    {
        var floorMap = await fixture.Client.GetFromJsonAsync<FloorMapDto>(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/floor-map");
        return floorMap!.Seats.Single();
    }

    private static async Task<Guid> SeatIdAsync(DevicePlayerFixture fixture)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.DeviceSeatAssignments
            .Where(assignment => assignment.DeviceId == fixture.Device.DeviceId && assignment.DetachedAtUtc == null)
            .Select(assignment => assignment.SeatId)
            .SingleAsync();
    }
}
