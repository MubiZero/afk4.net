using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Analytics;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Devices;

/// <summary>
/// Расписание ПК клуба (docs/operations/pc-health.md): к открытию спящие ПК будятся по сети через включённого
/// соседа, к ночи свободные выключаются — и только свободные, и один раз за окно.
/// </summary>
public sealed class PcScheduleTests
{
    [Theory]
    [InlineData(9 * 60, 9 * 60, 0)]
    [InlineData(9 * 60, 9 * 60 + 5, 5)]
    [InlineData(23 * 60 + 50, 5, 15)]
    [InlineData(9 * 60, 8 * 60 + 59, 1439)]
    public void MinutesSince_GoesAcrossMidnight(int scheduled, int current, int expected) =>
        Assert.Equal(expected, PcSchedule.MinutesSince(scheduled, current));

    [Fact]
    public async Task AtWakeTime_ASleepingPcIsWokenByAPoweredNeighbour()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var now = DateTimeOffset.UtcNow;
        var sleeping = Guid.NewGuid();
        var neighbour = Guid.NewGuid();
        await SeedAsync(factory, wakeNow: true, shutdownNow: false,
            Pc(sleeping, lastHeartbeat: now.AddHours(-8)), Pc(neighbour, lastHeartbeat: now.AddSeconds(-10)));

        await RunAsync(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        var command = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().DeviceCommands.SingleAsync();
        Assert.Equal((neighbour, DeviceCommandTypeNames.WakeNeighbor), (command.DeviceId, command.Type));
        Assert.Contains(sleeping.ToString("D"), command.PayloadJson);
    }

    // Гость ещё играет — его ПК не трогаем; свободный выключаем, и только один раз за окно.
    [Fact]
    public async Task AtShutdownTime_OnlyFreePcsAreTurnedOff_Once()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var now = DateTimeOffset.UtcNow;
        var free = Guid.NewGuid();
        var playing = Guid.NewGuid();
        await SeedAsync(factory, wakeNow: false, shutdownNow: true,
            Pc(free, lastHeartbeat: now.AddSeconds(-10)), Pc(playing, lastHeartbeat: now.AddSeconds(-10)));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            db.Zones.Add(new ZoneEntity { ZoneId = TestIds.ZoneId, OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId, Name = "Hall", SortOrder = 1, CreatedAtUtc = now });
            db.Seats.Add(new SeatEntity { SeatId = TestIds.SeatId, OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId, ZoneId = TestIds.ZoneId, Name = "PC-01", SortOrder = 1, CreatedAtUtc = now });
            db.Sessions.Add(new SessionEntity
            {
                SessionId = Guid.NewGuid(), OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId, SeatId = TestIds.SeatId,
                DeviceId = playing, PlayerKind = "guest", TariffRuleVersionId = "tariff", BillingMode = "prepaid_cash",
                State = SessionStateNames.Active, RequestedAtUtc = now, StartedAtUtc = now, EndsAtUtc = now.AddHours(1), UpdatedAtUtc = now, Version = 1
            });
            await db.SaveChangesAsync();
        }

        await RunAsync(factory);
        await RunAsync(factory);

        await using var check = factory.Services.CreateAsyncScope();
        var commands = await check.ServiceProvider.GetRequiredService<PlatformDbContext>().DeviceCommands.ToListAsync();
        var shutdown = Assert.Single(commands);
        Assert.Equal((free, DeviceCommandTypeNames.Shutdown), (shutdown.DeviceId, shutdown.Type));
    }

    private static DeviceEntity Pc(Guid deviceId, DateTimeOffset lastHeartbeat) => new()
    {
        DeviceId = deviceId, OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId,
        MachineName = $"PC-{deviceId.ToString()[..4]}", AgentVersion = "0.1.0", ShellVersion = "0.1.0", EnrolledAtUtc = lastHeartbeat,
        IsLocked = true, IsOnline = true, LastHeartbeatAtUtc = lastHeartbeat,
        NetworkMacAddress = $"AA-BB-CC-DD-{deviceId.ToString()[..2].ToUpperInvariant()}-01", NetworkSubnet = "192.168.1.0/24",
        NetworkBroadcastAddress = "192.168.1.255"
    };

    private static async Task SeedAsync(PlatformApiFactory factory, bool wakeNow, bool shutdownNow, params DeviceEntity[] pcs)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var branch = await db.Branches.SingleAsync(row => row.BranchId == TestIds.BranchId);
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, BranchLocalTime.ResolveZone(branch.PreferredTimeZone));
        var minute = local.Hour * 60 + local.Minute;
        db.BranchProtectionProfiles.Add(new BranchProtectionProfileEntity
        {
            BranchId = TestIds.BranchId, OrganizationId = TestIds.OrganizationId, Version = 1,
            WakeAtMinuteOfDay = wakeNow ? minute : null, ShutdownAtMinuteOfDay = shutdownNow ? minute : null,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        db.Devices.AddRange(pcs);
        await db.SaveChangesAsync();
    }

    private static async Task RunAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PcSchedule>().RunOnceAsync(CancellationToken.None);
    }
}
