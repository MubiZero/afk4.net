using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Updates;
using AFK4.Shared.Contracts.Updates;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class EfUpdateServiceTests
{
    private static readonly Guid ActorStaffUserId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid OtherDeviceId = Guid.Parse("99999999-9999-4999-8999-999999999999");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-14T13:00:00Z");

    [Fact]
    public async Task RegisterPackageAsync_RequiresSignedPackageMetadata()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var request = PackageRequest() with { Sha256 = " " };

        var result = await service.RegisterPackageAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("Package SHA-256 hash is required.", result.Error);
        Assert.Empty(await db.UpdatePackages.ToListAsync());
    }

    [Fact]
    public async Task RegisterPackageAsync_RejectsDuplicateComponentVersionChannelInBranch()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var first = PackageRequest();
        var duplicate = first with { ReleaseNotes = "same bits again" };

        var firstResult = await service.RegisterPackageAsync(TestIds.BranchId, ActorStaffUserId, first, CancellationToken.None);
        var secondResult = await service.RegisterPackageAsync(TestIds.BranchId, ActorStaffUserId, duplicate, CancellationToken.None);

        Assert.True(firstResult.Succeeded);
        Assert.False(secondResult.Succeeded);
        Assert.Equal("An update package already exists for this component, version, and channel.", secondResult.Error);
        Assert.Single(await db.UpdatePackages.ToListAsync());
    }

    [Fact]
    public async Task CreateRolloutAsync_StoresDeviceTargetsForPackage()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var package = await RegisterPackageAsync(service);
        var request = new CreateUpdateRolloutRequest(
            TestIds.OrganizationId,
            package.UpdatePackageId,
            UpdateChannelNames.Beta,
            UpdateTargetKindNames.Device,
            [TestIds.DeviceId, OtherDeviceId],
            BatchPercent: 50,
            StartsAtUtc: Now.AddMinutes(10),
            Reason: "Beta rollout.");

        var result = await service.CreateRolloutAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal(UpdateRolloutStateNames.Active, result.Response.State);
        Assert.Equal(UpdateTargetKindNames.Device, result.Response.TargetKind);
        Assert.Equal(2, result.Response.TargetDeviceIds.Count);
        Assert.Equal(2, await db.UpdateRolloutTargets.CountAsync());
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ReturnsActiveRolloutForTargetedDeviceOnly()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        await SeedDeviceAsync(db, TestIds.DeviceId);
        await SeedDeviceAsync(db, OtherDeviceId);
        var package = await RegisterPackageAsync(service);
        await service.CreateRolloutAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new CreateUpdateRolloutRequest(
                TestIds.OrganizationId,
                package.UpdatePackageId,
                UpdateChannelNames.Beta,
                UpdateTargetKindNames.Device,
                [TestIds.DeviceId],
                BatchPercent: 100,
                StartsAtUtc: Now,
                Reason: "Targeted device."),
            CancellationToken.None);

        var targeted = await service.CheckForUpdatesAsync(
            new DeviceUpdateCheckRequest(
                TestIds.OrganizationId,
                TestIds.BranchId,
                TestIds.DeviceId,
                UpdateChannelNames.Beta,
                Now.AddMinutes(1),
                [new DeviceComponentVersionDto(UpdateComponentNames.AgentService, "1.2.2")]),
            CancellationToken.None);
        var other = await service.CheckForUpdatesAsync(
            new DeviceUpdateCheckRequest(
                TestIds.OrganizationId,
                TestIds.BranchId,
                OtherDeviceId,
                UpdateChannelNames.Beta,
                Now.AddMinutes(1),
                [new DeviceComponentVersionDto(UpdateComponentNames.AgentService, "1.2.2")]),
            CancellationToken.None);

        Assert.True(targeted.Succeeded);
        Assert.NotNull(targeted.Response);
        var update = Assert.Single(targeted.Response.Updates);
        Assert.Equal(package.UpdatePackageId, update.UpdatePackageId);
        Assert.Equal("1.2.3", update.Version);
        Assert.True(other.Succeeded);
        Assert.NotNull(other.Response);
        Assert.Empty(other.Response.Updates);
    }

    [Fact]
    public async Task ReportStatusAsync_UpsertsDevicePackageStatus()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        await SeedDeviceAsync(db, TestIds.DeviceId);
        var package = await RegisterPackageAsync(service);
        var rollout = await service.CreateRolloutAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new CreateUpdateRolloutRequest(
                TestIds.OrganizationId,
                package.UpdatePackageId,
                UpdateChannelNames.Beta,
                UpdateTargetKindNames.Branch,
                [],
                BatchPercent: 100,
                StartsAtUtc: Now,
                Reason: "Stable rollout."),
            CancellationToken.None);
        Assert.True(rollout.Succeeded);
        Assert.NotNull(rollout.Response);
        var firstReport = StatusReport(rollout.Response.UpdateRolloutId, package.UpdatePackageId, UpdateStatusNames.Downloading, "download started");
        var secondReport = firstReport with
        {
            Status = UpdateStatusNames.Installed,
            Message = "installed",
            ObservedAtUtc = Now.AddMinutes(5)
        };

        var first = await service.ReportStatusAsync(firstReport, CancellationToken.None);
        var second = await service.ReportStatusAsync(secondReport, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotNull(second.Response);
        Assert.Equal(UpdateStatusNames.Installed, second.Response.Status);
        Assert.Single(await db.DeviceUpdateStatuses.ToListAsync());
        var stored = await db.DeviceUpdateStatuses.SingleAsync();
        Assert.Equal(UpdateStatusNames.Installed, stored.Status);
        Assert.Equal(Now.AddMinutes(5), stored.UpdatedAtUtc);
    }

    [Fact]
    public async Task ListRolloutStatusesAsync_ReturnsRolloutsWithDeviceStatusSnapshots()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        await SeedDeviceAsync(db, TestIds.DeviceId);
        await SeedDeviceAsync(db, OtherDeviceId);
        var package = await RegisterPackageAsync(service);
        var rollout = await service.CreateRolloutAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new CreateUpdateRolloutRequest(
                TestIds.OrganizationId,
                package.UpdatePackageId,
                UpdateChannelNames.Beta,
                UpdateTargetKindNames.Device,
                [TestIds.DeviceId, OtherDeviceId],
                BatchPercent: 50,
                StartsAtUtc: Now,
                Reason: "Targeted rollout."),
            CancellationToken.None);
        Assert.True(rollout.Succeeded);
        Assert.NotNull(rollout.Response);
        await service.ReportStatusAsync(
            StatusReport(
                rollout.Response.UpdateRolloutId,
                package.UpdatePackageId,
                UpdateStatusNames.Installing,
                "install started"),
            CancellationToken.None);

        var result = await service.ListRolloutStatusesAsync(
            TestIds.OrganizationId,
            TestIds.BranchId,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        var readBack = Assert.Single(result.Response);
        Assert.Equal(rollout.Response.UpdateRolloutId, readBack.UpdateRolloutId);
        Assert.Equal(2, readBack.TargetDeviceIds.Count);
        var status = Assert.Single(readBack.DeviceStatuses);
        Assert.Equal(TestIds.DeviceId, status.DeviceId);
        Assert.Equal("1.2.2", status.InstalledVersion);
        Assert.Equal("1.2.3", status.TargetVersion);
        Assert.Equal(UpdateStatusNames.Installing, status.Status);
    }

    [Fact]
    public async Task ChangePackageStateAsync_ValidatesRegisteredPackage()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var package = await RegisterPackageAsync(service);

        var result = await service.ChangePackageStateAsync(
            TestIds.OrganizationId,
            TestIds.BranchId,
            package.UpdatePackageId,
            new UpdatePackageStateChangeRequest(
                TestIds.OrganizationId,
                UpdatePackageStateNames.Validated,
                "Signature and hash verified."),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal(UpdatePackageStateNames.Validated, result.Response.State);
        Assert.Equal(UpdatePackageStateNames.Validated, (await db.UpdatePackages.SingleAsync()).State);
    }

    [Fact]
    public async Task ChangeRolloutStateAsync_PausedRolloutIsNotOfferedToDevice()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        await SeedDeviceAsync(db, TestIds.DeviceId);
        var package = await RegisterPackageAsync(service);
        var rollout = await service.CreateRolloutAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new CreateUpdateRolloutRequest(
                TestIds.OrganizationId,
                package.UpdatePackageId,
                UpdateChannelNames.Beta,
                UpdateTargetKindNames.Branch,
                [],
                BatchPercent: 100,
                StartsAtUtc: Now,
                Reason: "Branch rollout."),
            CancellationToken.None);
        Assert.True(rollout.Succeeded);
        Assert.NotNull(rollout.Response);

        var paused = await service.ChangeRolloutStateAsync(
            TestIds.OrganizationId,
            TestIds.BranchId,
            rollout.Response.UpdateRolloutId,
            new UpdateRolloutStateChangeRequest(
                TestIds.OrganizationId,
                UpdateRolloutStateNames.Paused,
                "Pause rollout while investigating failures."),
            CancellationToken.None);
        var check = await service.CheckForUpdatesAsync(
            new DeviceUpdateCheckRequest(
                TestIds.OrganizationId,
                TestIds.BranchId,
                TestIds.DeviceId,
                UpdateChannelNames.Beta,
                Now.AddMinutes(1),
                [new DeviceComponentVersionDto(UpdateComponentNames.AgentService, "1.2.2")]),
            CancellationToken.None);

        Assert.True(paused.Succeeded);
        Assert.NotNull(paused.Response);
        Assert.Equal(UpdateRolloutStateNames.Paused, paused.Response.State);
        Assert.True(check.Succeeded);
        Assert.NotNull(check.Response);
        Assert.Empty(check.Response.Updates);
    }

    [Fact]
    public async Task ChangeRolloutStateAsync_RejectsTerminalRolloutTransition()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var package = await RegisterPackageAsync(service);
        var rollout = await service.CreateRolloutAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new CreateUpdateRolloutRequest(
                TestIds.OrganizationId,
                package.UpdatePackageId,
                UpdateChannelNames.Beta,
                UpdateTargetKindNames.Branch,
                [],
                BatchPercent: 100,
                StartsAtUtc: Now,
                Reason: "Branch rollout."),
            CancellationToken.None);
        Assert.True(rollout.Succeeded);
        Assert.NotNull(rollout.Response);

        var completed = await service.ChangeRolloutStateAsync(
            TestIds.OrganizationId,
            TestIds.BranchId,
            rollout.Response.UpdateRolloutId,
            new UpdateRolloutStateChangeRequest(
                TestIds.OrganizationId,
                UpdateRolloutStateNames.Completed,
                "All devices installed."),
            CancellationToken.None);
        var restart = await service.ChangeRolloutStateAsync(
            TestIds.OrganizationId,
            TestIds.BranchId,
            rollout.Response.UpdateRolloutId,
            new UpdateRolloutStateChangeRequest(
                TestIds.OrganizationId,
                UpdateRolloutStateNames.Active,
                "Restart completed rollout."),
            CancellationToken.None);

        Assert.True(completed.Succeeded);
        Assert.NotNull(completed.Response);
        Assert.Equal(UpdateRolloutStateNames.Completed, completed.Response.State);
        Assert.NotNull(completed.Response.CompletedAtUtc);
        Assert.False(restart.Succeeded);
        Assert.Equal("Completed, rolled-back, and cancelled rollouts are terminal.", restart.Error);
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PlatformDbContext(options);
    }

    private static EfUpdateService CreateService(PlatformDbContext db)
    {
        return new EfUpdateService(db, new FixedTimeProvider(Now));
    }

    private static CreateUpdatePackageRequest PackageRequest()
    {
        return new CreateUpdatePackageRequest(
            TestIds.OrganizationId,
            UpdateComponentNames.AgentService,
            "1.2.3",
            UpdateChannelNames.Beta,
            "https://updates.afk4.test/agent/1.2.3/agent.msi",
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            "base64-signature",
            "ed25519",
            SizeBytes: 42_000_000,
            ReleaseNotes: "Agent update.");
    }

    private static async Task<UpdatePackageDto> RegisterPackageAsync(EfUpdateService service)
    {
        var result = await service.RegisterPackageAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            PackageRequest(),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);

        return result.Response;
    }

    private static DeviceUpdateStatusReportRequest StatusReport(
        Guid rolloutId,
        Guid packageId,
        string status,
        string message)
    {
        return new DeviceUpdateStatusReportRequest(
            TestIds.OrganizationId,
            TestIds.BranchId,
            TestIds.DeviceId,
            rolloutId,
            packageId,
            UpdateComponentNames.AgentService,
            InstalledVersion: "1.2.2",
            TargetVersion: "1.2.3",
            status,
            message,
            Now.AddMinutes(1));
    }

    private static async Task SeedDeviceAsync(PlatformDbContext db, Guid deviceId)
    {
        db.Devices.Add(new DeviceEntity
        {
            DeviceId = deviceId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            MachineName = deviceId == TestIds.DeviceId ? "PC-001" : "PC-002",
            AgentVersion = "1.2.2",
            ShellVersion = "1.2.2",
            EnrolledAtUtc = Now.AddDays(-1),
            LastHeartbeatAtUtc = Now,
            IsOnline = true,
            IsLocked = true
        });
        await db.SaveChangesAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
