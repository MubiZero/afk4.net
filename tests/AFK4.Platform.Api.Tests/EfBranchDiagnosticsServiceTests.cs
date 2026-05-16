using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Diagnostics;
using AFK4.Shared.Contracts.Updates;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class EfBranchDiagnosticsServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-16T10:00:00Z");
    private static readonly Guid OtherBranchId = Guid.Parse("99999999-9999-4999-8999-999999999999");
    private static readonly Guid OnlineDeviceId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid StaleDeviceId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid NeverSeenDeviceId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid OtherBranchDeviceId = Guid.Parse("44444444-4444-4444-8444-444444444444");

    [Fact]
    public async Task GetAsync_ReturnsBranchDiagnosticsFromExistingOperationalState()
    {
        await using var db = CreateDbContext();
        var service = new EfBranchDiagnosticsService(
            db,
            new FixedTimeProvider(Now),
            new BranchDiagnosticsOptions
            {
                StaleHeartbeatSeconds = 300,
                RecentFailureLimit = 5
            });
        SeedDevices(db);
        SeedCommands(db);
        SeedUpdates(db);
        await db.SaveChangesAsync();

        var result = await service.GetAsync(
            TestIds.OrganizationId,
            TestIds.BranchId,
            CancellationToken.None);

        Assert.Equal(TestIds.OrganizationId, result.OrganizationId);
        Assert.Equal(TestIds.BranchId, result.BranchId);
        Assert.Equal(Now, result.GeneratedAtUtc);
        Assert.Equal(3, result.DeviceSummary.TotalDevices);
        Assert.Equal(2, result.DeviceSummary.OnlineDevices);
        Assert.Equal(2, result.DeviceSummary.LockedDevices);
        Assert.Equal(2, result.DeviceSummary.StaleDevices);
        Assert.Equal(300, result.DeviceSummary.StaleThresholdSeconds);
        Assert.Equal(Now.AddMinutes(-1), result.DeviceSummary.NewestHeartbeatAtUtc);
        Assert.Equal(2, result.StaleDevices.Count);
        Assert.Contains(result.StaleDevices, device => device.DeviceId == StaleDeviceId && device.LastHeartbeatAgeSeconds == 600);
        Assert.Contains(result.StaleDevices, device => device.DeviceId == NeverSeenDeviceId && device.LastHeartbeatAgeSeconds is null);

        Assert.Equal(1, result.CommandSummary.PendingCommands);
        Assert.Equal(1, result.CommandSummary.FailedCommands);
        var failedCommand = Assert.Single(result.CommandSummary.RecentFailures);
        Assert.Equal(StaleDeviceId, failedCommand.DeviceId);
        Assert.Equal("PC-STALE", failedCommand.MachineName);
        Assert.Equal("unlock", failedCommand.Type);

        Assert.Equal(1, result.UpdateSummary.ActiveRollouts);
        Assert.Equal(1, result.UpdateSummary.InstallingDevices);
        Assert.Equal(1, result.UpdateSummary.FailedDevices);
        Assert.Equal(1, result.UpdateSummary.RollbackDevices);
        var failedUpdate = Assert.Single(result.UpdateSummary.RecentFailures);
        Assert.Equal(StaleDeviceId, failedUpdate.DeviceId);
        Assert.Equal("gaming-pc", failedUpdate.Component);
        Assert.Equal("install failed", failedUpdate.Message);
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PlatformDbContext(options);
    }

    private static void SeedDevices(PlatformDbContext db)
    {
        db.Devices.AddRange(
            new DeviceEntity
            {
                DeviceId = OnlineDeviceId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                MachineName = "PC-ONLINE",
                AgentVersion = "0.1.0",
                ShellVersion = "0.1.0",
                EnrolledAtUtc = Now.AddDays(-1),
                LastHeartbeatAtUtc = Now.AddMinutes(-1),
                IsOnline = true,
                IsLocked = true
            },
            new DeviceEntity
            {
                DeviceId = StaleDeviceId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                MachineName = "PC-STALE",
                AgentVersion = "0.1.0",
                ShellVersion = "0.1.0",
                EnrolledAtUtc = Now.AddDays(-1),
                LastHeartbeatAtUtc = Now.AddMinutes(-10),
                IsOnline = true,
                IsLocked = false
            },
            new DeviceEntity
            {
                DeviceId = NeverSeenDeviceId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                MachineName = "PC-NEVER-SEEN",
                AgentVersion = "0.1.0",
                ShellVersion = "0.1.0",
                EnrolledAtUtc = Now.AddDays(-1),
                LastHeartbeatAtUtc = null,
                IsOnline = false,
                IsLocked = true
            },
            new DeviceEntity
            {
                DeviceId = OtherBranchDeviceId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = OtherBranchId,
                MachineName = "PC-OTHER",
                AgentVersion = "0.1.0",
                ShellVersion = "0.1.0",
                EnrolledAtUtc = Now.AddDays(-1),
                LastHeartbeatAtUtc = Now.AddMinutes(-30),
                IsOnline = true,
                IsLocked = true
            });
    }

    private static void SeedCommands(PlatformDbContext db)
    {
        db.DeviceCommands.AddRange(
            new DeviceCommandEntity
            {
                CommandId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
                DeviceId = OnlineDeviceId,
                Type = "lock",
                PayloadJson = "{}",
                Status = "Pending",
                CreatedAtUtc = Now.AddMinutes(-2),
                UpdatedAtUtc = Now.AddMinutes(-2)
            },
            new DeviceCommandEntity
            {
                CommandId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
                DeviceId = StaleDeviceId,
                Type = "unlock",
                PayloadJson = "{}",
                Status = "Failed",
                Message = "timed out",
                CreatedAtUtc = Now.AddMinutes(-6),
                UpdatedAtUtc = Now.AddMinutes(-3)
            },
            new DeviceCommandEntity
            {
                CommandId = Guid.Parse("cccccccc-cccc-4ccc-cccc-cccccccccccc"),
                DeviceId = OtherBranchDeviceId,
                Type = "lock",
                PayloadJson = "{}",
                Status = "Failed",
                Message = "wrong branch",
                CreatedAtUtc = Now.AddMinutes(-7),
                UpdatedAtUtc = Now.AddMinutes(-4)
            });
    }

    private static void SeedUpdates(PlatformDbContext db)
    {
        var activeRolloutId = Guid.Parse("dddddddd-dddd-4ddd-dddd-dddddddddddd");
        var pausedRolloutId = Guid.Parse("eeeeeeee-eeee-4eee-eeee-eeeeeeeeeeee");
        var packageId = Guid.Parse("ffffffff-ffff-4fff-ffff-ffffffffffff");

        db.UpdateRollouts.AddRange(
            new UpdateRolloutEntity
            {
                UpdateRolloutId = activeRolloutId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                UpdatePackageId = packageId,
                Component = "gaming-pc",
                Version = "0.1.0",
                Channel = "internal",
                State = UpdateRolloutStateNames.Active,
                TargetKind = UpdateTargetKindNames.Branch,
                BatchPercent = 100,
                Reason = "test",
                CreatedByStaffUserId = TestIds.TechnicianStaffUserId,
                CreatedAtUtc = Now.AddMinutes(-20),
                StartsAtUtc = Now.AddMinutes(-20)
            },
            new UpdateRolloutEntity
            {
                UpdateRolloutId = pausedRolloutId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                UpdatePackageId = packageId,
                Component = "operator-app",
                Version = "0.1.0",
                Channel = "internal",
                State = UpdateRolloutStateNames.Paused,
                TargetKind = UpdateTargetKindNames.Branch,
                BatchPercent = 100,
                Reason = "test",
                CreatedByStaffUserId = TestIds.TechnicianStaffUserId,
                CreatedAtUtc = Now.AddMinutes(-20),
                StartsAtUtc = Now.AddMinutes(-20)
            });

        db.DeviceUpdateStatuses.AddRange(
            new DeviceUpdateStatusEntity
            {
                DeviceUpdateStatusId = Guid.NewGuid(),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                DeviceId = OnlineDeviceId,
                UpdateRolloutId = activeRolloutId,
                UpdatePackageId = packageId,
                Component = "gaming-pc",
                InstalledVersion = "0.0.9",
                TargetVersion = "0.1.0",
                Status = UpdateStatusNames.Installing,
                Message = "installing",
                FirstReportedAtUtc = Now.AddMinutes(-5),
                UpdatedAtUtc = Now.AddMinutes(-4)
            },
            new DeviceUpdateStatusEntity
            {
                DeviceUpdateStatusId = Guid.NewGuid(),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                DeviceId = StaleDeviceId,
                UpdateRolloutId = activeRolloutId,
                UpdatePackageId = packageId,
                Component = "gaming-pc",
                InstalledVersion = "0.0.9",
                TargetVersion = "0.1.0",
                Status = UpdateStatusNames.Failed,
                Message = "install failed",
                FirstReportedAtUtc = Now.AddMinutes(-6),
                UpdatedAtUtc = Now.AddMinutes(-3)
            },
            new DeviceUpdateStatusEntity
            {
                DeviceUpdateStatusId = Guid.NewGuid(),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                DeviceId = NeverSeenDeviceId,
                UpdateRolloutId = activeRolloutId,
                UpdatePackageId = packageId,
                Component = "gaming-pc",
                InstalledVersion = "0.0.9",
                TargetVersion = "0.1.0",
                Status = UpdateStatusNames.RollbackStarted,
                Message = "rollback",
                FirstReportedAtUtc = Now.AddMinutes(-7),
                UpdatedAtUtc = Now.AddMinutes(-2)
            },
            new DeviceUpdateStatusEntity
            {
                DeviceUpdateStatusId = Guid.NewGuid(),
                OrganizationId = TestIds.OrganizationId,
                BranchId = OtherBranchId,
                DeviceId = OtherBranchDeviceId,
                UpdateRolloutId = activeRolloutId,
                UpdatePackageId = packageId,
                Component = "gaming-pc",
                InstalledVersion = "0.0.9",
                TargetVersion = "0.1.0",
                Status = UpdateStatusNames.Failed,
                Message = "wrong branch",
                FirstReportedAtUtc = Now.AddMinutes(-6),
                UpdatedAtUtc = Now.AddMinutes(-1)
            });
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
