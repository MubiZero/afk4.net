using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Shared.Contracts.Devices;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class EfDeviceEnrollmentServiceTests
{
    [Fact]
    public async Task EnrollmentAndCredential_PersistAcrossServiceInstances()
    {
        var options = CreateOptions();
        var organizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
        var branchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
        DeviceEnrollmentCodeDto code;
        DeviceEnrollmentResponse enrollment;

        await using (var db = new PlatformDbContext(options))
        {
            var service = new EfDeviceEnrollmentService(db, TimeProvider.System, new EfPlanLimitGuard(db));
            code = await service.CreateEnrollmentCodeAsync(
                branchId,
                new CreateDeviceEnrollmentCodeRequest(organizationId, ExpiresInSeconds: 300),
                CancellationToken.None);
        }

        await using (var db = new PlatformDbContext(options))
        {
            var service = new EfDeviceEnrollmentService(db, TimeProvider.System, new EfPlanLimitGuard(db));
            var result = await service.EnrollAsync(
                new DeviceEnrollmentRequest(
                    OrganizationId: organizationId,
                    BranchId: branchId,
                    EnrollmentCode: code.Code,
                    MachineName: "PC-001",
                    AgentVersion: "0.1.0",
                    ShellVersion: "0.1.0",
                    RequestedAtUtc: DateTimeOffset.Parse("2026-05-12T00:01:00Z")),
                CancellationToken.None);

            Assert.True(result.Succeeded);
            enrollment = Assert.IsType<DeviceEnrollmentResponse>(result.Response);
        }

        await using (var db = new PlatformDbContext(options))
        {
            var validator = new EfDeviceEnrollmentService(db, TimeProvider.System, new EfPlanLimitGuard(db));

            Assert.True(validator.Validate(organizationId, branchId, enrollment.DeviceId, enrollment.CredentialSecret));
            Assert.False(validator.Validate(organizationId, branchId, enrollment.DeviceId, "wrong-secret"));
            Assert.True(await db.Devices.AnyAsync(device => device.DeviceId == enrollment.DeviceId));
            Assert.True(await db.DeviceCredentials.AnyAsync(credential => credential.DeviceId == enrollment.DeviceId));
            Assert.True(await db.DeviceEnrollmentCodes.AnyAsync(candidate => candidate.Code == code.Code && candidate.ConsumedAtUtc != null));
        }
    }

    private static DbContextOptions<PlatformDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
    }
}
