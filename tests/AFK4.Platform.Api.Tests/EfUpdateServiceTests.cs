using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Updates;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Platform.Updates;
using AFK4.Shared.Contracts.Updates;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class EfUpdateServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-29T12:00:00Z");
    private static readonly Guid RolloutId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid EligibleDeviceId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid IneligibleDeviceId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    [Theory]
    [InlineData("11111111-1111-4111-8111-111111111111", 70)]
    [InlineData("22222222-2222-4222-8222-222222222222", 84)]
    [InlineData("33333333-3333-4333-8333-333333333333", 2)]
    public void GetBucket_UsesStableRfc4122Vector(string deviceId, int expected)
    {
        Assert.Equal(expected, UpdateRolloutBucket.GetBucket(RolloutId, Guid.Parse(deviceId)));
    }

    [Fact]
    public async Task CheckForUpdatesAsync_AppliesBatchPercentDeterministically()
    {
        await using var db = CreateDbContext();
        SeedDevice(db, EligibleDeviceId, DeviceRoleNames.ManagerWorkstation);
        SeedDevice(db, IneligibleDeviceId, DeviceRoleNames.ManagerWorkstation);
        SeedRelease(db, RolloutId, "1.2.3", UpdateComponentNames.OrganizationAdmin, 10);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var eligible = await service.CheckForUpdatesAsync(CheckRequest(EligibleDeviceId), CancellationToken.None);
        var ineligible = await service.CheckForUpdatesAsync(CheckRequest(IneligibleDeviceId), CancellationToken.None);

        Assert.True(eligible.Succeeded);
        Assert.Equal("1.2.3", Assert.Single(eligible.Response!.Updates).Version);
        Assert.True(ineligible.Succeeded);
        Assert.Empty(ineligible.Response!.Updates);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ReturnsOnlyNewestEligibleVersionPerComponent()
    {
        await using var db = CreateDbContext();
        SeedDevice(db, EligibleDeviceId, DeviceRoleNames.ManagerWorkstation);
        SeedRelease(db, RolloutId, "1.2.3", UpdateComponentNames.OrganizationAdmin, 100);
        SeedRelease(db, Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"), "1.10.0", UpdateComponentNames.OrganizationAdmin, 100);
        await db.SaveChangesAsync();

        var result = await CreateService(db).CheckForUpdatesAsync(CheckRequest(EligibleDeviceId), CancellationToken.None);

        var update = Assert.Single(result.Response!.Updates);
        Assert.Equal("1.10.0", update.Version);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_DoesNotOfferRetiredOrUnvalidatedPackages()
    {
        await using var db = CreateDbContext();
        SeedDevice(db, EligibleDeviceId, DeviceRoleNames.ManagerWorkstation);
        SeedRelease(db, RolloutId, "1.2.3", UpdateComponentNames.OrganizationAdmin, 100, UpdatePackageStateNames.Retired);
        SeedRelease(db, Guid.NewGuid(), "1.3.0", UpdateComponentNames.OrganizationAdmin, 100, UpdatePackageStateNames.Registered);
        await db.SaveChangesAsync();

        var result = await CreateService(db).CheckForUpdatesAsync(CheckRequest(EligibleDeviceId), CancellationToken.None);

        Assert.Empty(result.Response!.Updates);
    }

    [Theory]
    [InlineData(PlatformUpdateTargetKindNames.Branch)]
    [InlineData(PlatformUpdateTargetKindNames.Device)]
    public async Task CheckForUpdatesAsync_MatchesBranchAndDeviceTargetsWithoutDuplicatedOrganizationId(string targetKind)
    {
        await using var db = CreateDbContext();
        SeedDevice(db, EligibleDeviceId, DeviceRoleNames.ManagerWorkstation);
        SeedRelease(db, RolloutId, "1.2.3", UpdateComponentNames.OrganizationAdmin, 100);
        var target = db.UpdateRolloutTargets.Local.Single();
        target.TargetKind = targetKind;
        target.OrganizationId = null;
        target.BranchId = TestIds.BranchId;
        target.DeviceId = targetKind == PlatformUpdateTargetKindNames.Device ? EligibleDeviceId : null;
        await db.SaveChangesAsync();

        var result = await CreateService(db).CheckForUpdatesAsync(CheckRequest(EligibleDeviceId), CancellationToken.None);

        Assert.Single(result.Response!.Updates);
    }

    [Fact]
    public async Task ReportStatusAsync_UpsertsAndReadOnlyBranchStatusReturnsSnapshot()
    {
        await using var db = CreateDbContext();
        SeedDevice(db, EligibleDeviceId, DeviceRoleNames.ManagerWorkstation);
        var packageId = SeedRelease(db, RolloutId, "1.2.3", UpdateComponentNames.OrganizationAdmin, 100);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var report = new DeviceUpdateStatusReportRequest(
            TestIds.OrganizationId, TestIds.BranchId, EligibleDeviceId, RolloutId, packageId,
            UpdateComponentNames.OrganizationAdmin, "1.2.2", "1.2.3", UpdateStatusNames.Installing,
            "Installing verified package.", Now.AddMinutes(1));

        var first = await service.ReportStatusAsync(report, CancellationToken.None);
        var second = await service.ReportStatusAsync(
            report with { Status = UpdateStatusNames.Installed, Message = "Installed.", ObservedAtUtc = Now.AddMinutes(2) },
            CancellationToken.None);
        var statuses = await service.ListRolloutStatusesAsync(TestIds.OrganizationId, TestIds.BranchId, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Single(await db.DeviceUpdateStatuses.ToListAsync());
        var snapshot = Assert.Single(Assert.Single(statuses.Response!).DeviceStatuses);
        Assert.Equal(UpdateStatusNames.Installed, snapshot.Status);
        Assert.Equal("Installed.", snapshot.Message);
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new PlatformDbContext(options);
    }

    private static EfUpdateService CreateService(PlatformDbContext db) =>
        new(db, new FixedTimeProvider(Now));

    private static void SeedDevice(PlatformDbContext db, Guid deviceId, string role)
    {
        if (!db.Branches.Local.Any(branch => branch.BranchId == TestIds.BranchId))
        {
            db.Branches.Add(new BranchEntity
            {
                BranchId = TestIds.BranchId,
                OrganizationId = TestIds.OrganizationId,
                Name = "Update branch",
                PreferredTimeZone = "Asia/Dushanbe",
                OrganizationAdminMaintenanceWindowStart = new TimeOnly(4, 0),
                OrganizationAdminMaintenanceWindowEnd = new TimeOnly(5, 0),
                CreatedAtUtc = Now
            });
        }
        db.Devices.Add(new DeviceEntity
        {
            DeviceId = deviceId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            MachineName = deviceId.ToString("N"),
            DisplayName = "Update device",
            DevicePublicKey = "test-key",
            Role = role,
            EnrollmentState = DeviceEnrollmentStateNames.Approved,
            EnrolledAtUtc = Now
        });
    }

    private static Guid SeedRelease(
        PlatformDbContext db,
        Guid rolloutId,
        string version,
        string component,
        int batchPercent,
        string packageState = UpdatePackageStateNames.Validated)
    {
        var packageId = Guid.NewGuid();
        db.UpdatePackages.Add(new UpdatePackageEntity
        {
            UpdatePackageId = packageId,
            Component = component,
            Version = version,
            Channel = UpdateChannelNames.Stable,
            ArtifactUri = $"https://updates.afk4.test/{component}/{version}/package.msi",
            Sha256 = new string('a', 64),
            Signature = "signature",
            SignatureAlgorithm = UpdatePackageSignatureAlgorithmNames.EcdsaP256Sha256IeeeP1363,
            SizeBytes = 42,
            State = packageState,
            ReleaseNotes = "Release.",
            CreatedByPlatformAdminUserId = Guid.Parse("44444444-4444-4444-8444-444444444444"),
            CreatedAtUtc = Now
        });
        db.UpdateRollouts.Add(new UpdateRolloutEntity
        {
            UpdateRolloutId = rolloutId,
            UpdatePackageId = packageId,
            Component = component,
            Version = version,
            Channel = UpdateChannelNames.Stable,
            State = UpdateRolloutStateNames.Active,
            TargetKind = PlatformUpdateTargetKindNames.Organization,
            BatchPercent = batchPercent,
            Reason = "Release.",
            CreatedByPlatformAdminUserId = Guid.Parse("44444444-4444-4444-8444-444444444444"),
            CreatedAtUtc = Now,
            StartsAtUtc = Now
        });
        db.UpdateRolloutTargets.Add(new UpdateRolloutTargetEntity
        {
            UpdateRolloutTargetId = Guid.NewGuid(),
            UpdateRolloutId = rolloutId,
            OrganizationId = TestIds.OrganizationId,
            TargetKind = PlatformUpdateTargetKindNames.Organization,
            CreatedAtUtc = Now
        });
        return packageId;
    }

    private static DeviceUpdateCheckRequest CheckRequest(Guid deviceId) => new(
        TestIds.OrganizationId,
        TestIds.BranchId,
        deviceId,
        UpdateChannelNames.Stable,
        Now.AddMinutes(1),
        [new DeviceComponentVersionDto(UpdateComponentNames.OrganizationAdmin, "1.2.2")]);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
