using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Devices;

/// <summary>
/// «Снять киоск» = ПК выходит из зала. Агент без учётки игрока сам докладывает об этом сердцебиением;
/// сервер хранит признак, карта его показывает, посадить такой ПК и слать ему команды для игрока
/// нельзя. Вернуть в зал — поставить киоск мастером заново: агент снова увидит его и признак снимется.
/// </summary>
public sealed class DeviceKioskAbsentTests
{
    [Fact]
    public async Task TheHeartbeat_WithoutAKiosk_TakesThePcOffTheFloor_AndAKioskBringsItBack()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();

        await fixture.HeartbeatAsync(kioskInstalled: false);
        var off = await SeatAsync(fixture);
        Assert.True(off.IsKioskAbsent);

        await fixture.HeartbeatAsync(kioskInstalled: true);
        Assert.False((await SeatAsync(fixture)).IsKioskAbsent);
    }

    [Fact]
    public async Task AnOlderAgentThatSaysNothing_DoesNotChangeTheFlag()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();

        await fixture.HeartbeatAsync(kioskInstalled: false);
        await fixture.HeartbeatAsync(kioskInstalled: null);
        Assert.True((await SeatAsync(fixture)).IsKioskAbsent);

        await fixture.HeartbeatAsync(kioskInstalled: true);
        await fixture.HeartbeatAsync(kioskInstalled: null);
        Assert.False((await SeatAsync(fixture)).IsKioskAbsent);
    }

    [Fact]
    public async Task TheMomentTheKioskWasLost_IsKept_WhileTheAgentKeepsSayingSo()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();

        await fixture.HeartbeatAsync(kioskInstalled: false);
        fixture.Clock.Advance(TimeSpan.FromHours(3));
        await fixture.HeartbeatAsync(kioskInstalled: false);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var device = await db.Devices.SingleAsync(candidate => candidate.DeviceId == fixture.Device.DeviceId);
        Assert.Equal(DevicePlayerFixture.Start, device.KioskAbsentSinceUtc);
    }

    /// <summary>Рабочее место менеджера учётки игрока не имеет никогда — это не повод уводить его из зала.</summary>
    [Fact]
    public async Task APcThatIsNotAGamingPc_IsNotTakenOffTheFloorForHavingNoKiosk()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            (await db.Devices.SingleAsync(candidate => candidate.DeviceId == fixture.Device.DeviceId)).Role = DeviceRoleNames.ManagerWorkstation;
            await db.SaveChangesAsync();
        }

        await fixture.HeartbeatAsync(kioskInstalled: false);

        Assert.False((await SeatAsync(fixture)).IsKioskAbsent);
    }

    [Fact]
    public async Task APcWithoutAKiosk_CannotStartASession_AndTheSeatingCodeIsHidden()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        var seatId = await SeatIdAsync(fixture);

        var off = await fixture.HeartbeatAsync(kioskInstalled: false);
        var refused = await StartAsync(fixture, seatId, "start-1");

        Assert.Null(off.SeatingCode);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains(DeviceCommandErrorCodeNames.KioskRemoved, await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        Assert.NotNull((await fixture.HeartbeatAsync(kioskInstalled: true)).SeatingCode);
        Assert.Equal(HttpStatusCode.OK, (await StartAsync(fixture, seatId, "start-2")).StatusCode);
    }

    [Theory]
    [InlineData(DeviceCommandTypeNames.Lock)]
    [InlineData(DeviceCommandTypeNames.Unlock)]
    [InlineData(DeviceCommandTypeNames.SignOut)]
    [InlineData(DeviceCommandTypeNames.MaintenanceOn)]
    public async Task CommandsThatNeedAPlayer_AreRefusedWithACode_OnAPcWithoutAKiosk(string type)
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        await fixture.HeartbeatAsync(kioskInstalled: false);

        var refused = await fixture.CommandAsync(type);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains(DeviceCommandErrorCodeNames.KioskRemoved, await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMessageNeedsAPlayerToo()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        await fixture.HeartbeatAsync(kioskInstalled: false);

        var refused = await fixture.CommandAsync(DeviceCommandTypeNames.Message, new Dictionary<string, string> { ["text"] = "Подойдите к стойке" });

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
    }

    /// <summary>Машиной без киоска управлять можно: перезагрузить, выключить, обновить защиту.</summary>
    [Theory]
    [InlineData(DeviceCommandTypeNames.Reboot)]
    [InlineData(DeviceCommandTypeNames.Shutdown)]
    [InlineData(DeviceCommandTypeNames.PolicyRefresh)]
    [InlineData(DeviceCommandTypeNames.MaintenanceOff)]
    public async Task MachineCommands_StillWork_OnAPcWithoutAKiosk(string type)
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        await fixture.HeartbeatAsync(kioskInstalled: false);

        Assert.Equal(HttpStatusCode.OK, (await fixture.CommandAsync(type)).StatusCode);
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
