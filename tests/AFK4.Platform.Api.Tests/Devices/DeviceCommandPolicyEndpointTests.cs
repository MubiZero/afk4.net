using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Devices;

/// <summary>
/// Команды администратора ПК (спека оболочки, §5.8): какие вообще бывают, когда их нельзя и
/// сколько раз агент их получает. Перезагрузка, отданная дважды, — это петля перезагрузок.
/// </summary>
public sealed class DeviceCommandPolicyEndpointTests
{
    [Fact]
    public async Task AnUnknownCommandType_IsRefusedByTheServer()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();

        var response = await fixture.CommandAsync("self-destruct");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(DeviceCommandErrorCodeNames.UnknownType, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheAgentCommandForWaking_CannotBeSentByAnAdminDirectly()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();

        var response = await fixture.CommandAsync(DeviceCommandTypeNames.WakeNeighbor, new() { ["mac"] = "AA-BB-CC-DD-EE-FF" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(DeviceCommandTypeNames.Reboot)]
    [InlineData(DeviceCommandTypeNames.Shutdown)]
    [InlineData(DeviceCommandTypeNames.MaintenanceOn)]
    public async Task PowerAndMaintenance_WaitUntilNobodyIsPlaying(string type)
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        await fixture.StartSessionAsync(fixture.PlayerAccountId);

        var response = await fixture.CommandAsync(type);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(DeviceCommandErrorCodeNames.ActiveSession, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARestart_IsHandedToTheAgentOnce()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();

        Assert.Equal(HttpStatusCode.OK, (await fixture.CommandAsync(DeviceCommandTypeNames.Reboot)).StatusCode);

        var first = await fixture.HeartbeatAsync();
        Assert.Single(first.Commands, command => command.Type == DeviceCommandTypeNames.Reboot);

        // Агент ещё не ответил — например, уже перезагружается. Второй раз ему её не отдают.
        var second = await fixture.HeartbeatAsync();
        Assert.DoesNotContain(second.Commands, command => command.Type == DeviceCommandTypeNames.Reboot);
    }

    [Fact]
    public async Task ARestartNobodyPickedUpInTenMinutes_Expires()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        await fixture.CommandAsync(DeviceCommandTypeNames.Shutdown);

        fixture.Clock.Advance(DeviceCommandPolicy.OneShotLifetime + TimeSpan.FromSeconds(1));
        var beat = await fixture.HeartbeatAsync();

        Assert.DoesNotContain(beat.Commands, command => command.Type == DeviceCommandTypeNames.Shutdown);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(DeviceCommandStatusNames.Expired, (await db.DeviceCommands.SingleAsync()).Status);
    }

    [Fact]
    public async Task AMessage_NeedsText()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.CommandAsync(DeviceCommandTypeNames.Message)).StatusCode);
        var sent = await fixture.CommandAsync(DeviceCommandTypeNames.Message, new() { ["text"] = "Через пять минут закрываемся" });

        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        Assert.Contains((await fixture.HeartbeatAsync()).Commands, command => command.Type == DeviceCommandTypeNames.Message);
    }

    [Fact]
    public void Maintenance_IsItsOwnRight_GivenToThoseWhoLookAfterThePcs()
    {
        Assert.Equal(OrganizationPermissionNames.MaintainDevice, DeviceCommandPolicy.RequiredPermission(DeviceCommandTypeNames.MaintenanceOn));
        foreach (var role in new[] { OrganizationRoleNames.OrganizationOwner, OrganizationRoleNames.BranchManager, OrganizationRoleNames.Technician })
        {
            Assert.Contains(OrganizationPermissionNames.MaintainDevice, OrganizationPermissionCatalog.GetPermissions([role]));
        }

        Assert.DoesNotContain(
            OrganizationPermissionNames.MaintainDevice,
            OrganizationPermissionCatalog.GetPermissions([OrganizationRoleNames.Operator]));
    }

    [Fact]
    public async Task WakingAPcThatNeverSaidItsAddress_IsRefused()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();

        var response = await fixture.CommandAsync(DeviceCommandTypeNames.Wake);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(DeviceCommandErrorCodeNames.WakeTargetUnknown, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WakingWithNobodyAwakeInTheSameNetwork_IsRefused()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        await fixture.HeartbeatAsync(macAddress: "AA-BB-CC-DD-EE-01", subnet: "192.168.1.0/24");
        var neighbour = await fixture.EnrollAnotherDeviceAsync();
        // Сосед есть, но в другой подсети: волшебный пакет через маршрутизатор не пройдёт.
        await fixture.HeartbeatAsync(device: neighbour, macAddress: "AA-BB-CC-DD-EE-02", subnet: "10.0.0.0/24");

        var response = await fixture.CommandAsync(DeviceCommandTypeNames.Wake);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(DeviceCommandErrorCodeNames.NoWakeHelper, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WakingAPc_AsksANeighbourInItsNetworkToSendTheMagicPacket()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        await fixture.HeartbeatAsync(macAddress: "AA-BB-CC-DD-EE-01", subnet: "192.168.1.0/24");
        var neighbour = await fixture.EnrollAnotherDeviceAsync();
        await fixture.HeartbeatAsync(device: neighbour, macAddress: "AA-BB-CC-DD-EE-02", subnet: "192.168.1.0/24");

        var response = await fixture.CommandAsync(DeviceCommandTypeNames.Wake);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var command = await response.Content.ReadFromJsonAsync<DeviceCommandDto>();
        Assert.Equal(DeviceCommandTypeNames.WakeNeighbor, command!.Type);
        Assert.Equal("AA-BB-CC-DD-EE-01", command.Payload["mac"]);
        Assert.Equal(fixture.Device.DeviceId.ToString("D"), command.Payload["targetDeviceId"]);

        var neighbourBeat = await fixture.HeartbeatAsync(device: neighbour);
        Assert.Contains(neighbourBeat.Commands, candidate => candidate.CommandId == command.CommandId);
    }
}
