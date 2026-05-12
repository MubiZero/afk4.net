using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Shared.Contracts.Devices;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class EfDeviceCredentialLifecycleServiceTests
{
    [Fact]
    public async Task RotateAsync_RevokesOldCredential()
    {
        var options = CreateOptions();
        var enrollment = await EnrollDeviceAsync(options);

        await using var db = new PlatformDbContext(options);
        var lifecycle = new EfDeviceCredentialLifecycleService(db, TimeProvider.System);
        var rotated = await lifecycle.RotateAsync(enrollment.DeviceId, CancellationToken.None);
        var validator = new EfDeviceEnrollmentService(db, TimeProvider.System);

        Assert.NotNull(rotated);
        Assert.Equal(enrollment.DeviceId, rotated.DeviceId);
        Assert.NotEqual(enrollment.CredentialId, rotated.CredentialId);
        Assert.False(validator.Validate(enrollment.OrganizationId, enrollment.BranchId, enrollment.DeviceId, enrollment.CredentialSecret));
        Assert.True(validator.Validate(rotated.OrganizationId, rotated.BranchId, rotated.DeviceId, rotated.CredentialSecret));
    }

    [Fact]
    public async Task RevokeAsync_InvalidatesCredential()
    {
        var options = CreateOptions();
        var enrollment = await EnrollDeviceAsync(options);

        await using var db = new PlatformDbContext(options);
        var lifecycle = new EfDeviceCredentialLifecycleService(db, TimeProvider.System);
        var revoked = await lifecycle.RevokeAsync(enrollment.DeviceId, enrollment.CredentialId, CancellationToken.None);
        var validator = new EfDeviceEnrollmentService(db, TimeProvider.System);

        Assert.NotNull(revoked);
        Assert.Equal(enrollment.CredentialId, revoked.CredentialId);
        Assert.False(validator.Validate(enrollment.OrganizationId, enrollment.BranchId, enrollment.DeviceId, enrollment.CredentialSecret));
    }

    private static async Task<DeviceEnrollmentResponse> EnrollDeviceAsync(DbContextOptions<PlatformDbContext> options)
    {
        await using var createCodeDb = new PlatformDbContext(options);
        var enrollmentService = new EfDeviceEnrollmentService(createCodeDb, TimeProvider.System);
        var code = await enrollmentService.CreateEnrollmentCodeAsync(
            TestIds.BranchId,
            new CreateDeviceEnrollmentCodeRequest(TestIds.OrganizationId, ExpiresInSeconds: 300),
            CancellationToken.None);

        await using var enrollDb = new PlatformDbContext(options);
        enrollmentService = new EfDeviceEnrollmentService(enrollDb, TimeProvider.System);
        var result = await enrollmentService.EnrollAsync(
            new DeviceEnrollmentRequest(
                OrganizationId: TestIds.OrganizationId,
                BranchId: TestIds.BranchId,
                EnrollmentCode: code.Code,
                MachineName: "PC-001",
                AgentVersion: "0.1.0",
                ShellVersion: "0.1.0",
                RequestedAtUtc: DateTimeOffset.Parse("2026-05-12T00:01:00Z")),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        return Assert.IsType<DeviceEnrollmentResponse>(result.Response);
    }

    private static DbContextOptions<PlatformDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
    }
}
