using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform.Entitlements;

public sealed class DeviceEnrollmentPlanLimitTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Enroll_RefusesWithNumbers_WhenBranchIsAtDeviceLimit()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var (organizationId, branchId) = await SeedOrganizationAsync(db, maxDevicesPerBranch: 1);
        SeedDevice(db, organizationId, branchId);
        var code = await SeedEnrollmentCodeAsync(db, organizationId, branchId);
        var service = scope.ServiceProvider.GetRequiredService<IDeviceEnrollmentService>();

        var result = await service.EnrollAsync(
            new DeviceEnrollmentRequest(organizationId, branchId, code, "PC-2", "1.0.0", "1.0.0", Now),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.PlanLimit);
        Assert.Equal(PlanLimitNames.DevicesPerBranch, result.PlanLimit!.LimitName);
        Assert.Equal(1, result.PlanLimit.Limit);
        Assert.Equal(1, result.PlanLimit.Current);
        Assert.Equal(1, await db.Devices.CountAsync());
    }

    [Fact]
    public async Task Enroll_ConsumesNothing_WhenRefusedByLimit()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var (organizationId, branchId) = await SeedOrganizationAsync(db, maxDevicesPerBranch: 1);
        SeedDevice(db, organizationId, branchId);
        var code = await SeedEnrollmentCodeAsync(db, organizationId, branchId);
        var service = scope.ServiceProvider.GetRequiredService<IDeviceEnrollmentService>();

        await service.EnrollAsync(
            new DeviceEnrollmentRequest(organizationId, branchId, code, "PC-2", "1.0.0", "1.0.0", Now),
            CancellationToken.None);

        // Отказ по лимиту не должен сжигать одноразовый код: клуб поднимет тариф и повторит
        // привязку тем же кодом, а не пойдёт выпрашивать новый.
        var stored = await db.DeviceEnrollmentCodes.SingleAsync();
        Assert.Null(stored.ConsumedAtUtc);
    }

    [Fact]
    public async Task Enroll_Succeeds_WhenBelowLimit()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var (organizationId, branchId) = await SeedOrganizationAsync(db, maxDevicesPerBranch: 2);
        SeedDevice(db, organizationId, branchId);
        var code = await SeedEnrollmentCodeAsync(db, organizationId, branchId);
        var service = scope.ServiceProvider.GetRequiredService<IDeviceEnrollmentService>();

        var result = await service.EnrollAsync(
            new DeviceEnrollmentRequest(organizationId, branchId, code, "PC-2", "1.0.0", "1.0.0", Now),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.PlanLimit);
    }

    private static async Task<(Guid OrganizationId, Guid BranchId)> SeedOrganizationAsync(
        PlatformDbContext db,
        int maxDevicesPerBranch)
    {
        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = organizationId,
            Slug = "club-" + organizationId.ToString("N")[..8],
            Name = "Клуб",
            Status = OrganizationStatusNames.Active,
            PlanCode = "growth",
            LimitsJson = OrganizationLimitsJson.Serialize(
                new OrganizationLimitsDto(null, maxDevicesPerBranch, null, null)),
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = branchId,
            OrganizationId = organizationId,
            Slug = "branch-" + branchId.ToString("N")[..8],
            Name = "Филиал",
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return (organizationId, branchId);
    }

    private static void SeedDevice(PlatformDbContext db, Guid organizationId, Guid branchId)
    {
        db.Devices.Add(new DeviceEntity
        {
            DeviceId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            MachineName = "PC-1",
            DisplayName = "PC-1",
            Role = DeviceRoleNames.GamingPc,
            EnrollmentState = DeviceEnrollmentStateNames.Approved,
            EnrolledAtUtc = Now
        });
        db.SaveChanges();
    }

    private static async Task<string> SeedEnrollmentCodeAsync(PlatformDbContext db, Guid organizationId, Guid branchId)
    {
        // EfDeviceEnrollmentService checks expiry against the real TimeProvider (PlatformApiFactory
        // does not stub it), so the expiry must be anchored to wall-clock now, not the fixed `Now`
        // used for seeding other timestamps.
        const string plainCode = "AFK4-TEST-0001";
        var utcNow = DateTimeOffset.UtcNow;
        db.DeviceEnrollmentCodes.Add(new DeviceEnrollmentCodeEntity
        {
            Code = DeviceCredentialSecrets.NormalizeEnrollmentCode(plainCode),
            OrganizationId = organizationId,
            BranchId = branchId,
            CreatedAtUtc = utcNow,
            ExpiresAtUtc = utcNow.AddMinutes(10),
            ConsumedAtUtc = null
        });
        await db.SaveChangesAsync();
        return plainCode;
    }
}
