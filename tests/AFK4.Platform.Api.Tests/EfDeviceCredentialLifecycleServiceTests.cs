using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Tests.Devices;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class EfDeviceCredentialLifecycleServiceTests
{
    [Fact]
    public async Task RotateAsync_RevokesOldCredential()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var enrollment = await TestDeviceEnrollment.EnrollDeviceAsync(client, TestIds.OrganizationId, TestIds.BranchId);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var lifecycle = new EfDeviceCredentialLifecycleService(db, TimeProvider.System);
        var rotated = await lifecycle.RotateAsync(enrollment.DeviceId, CancellationToken.None);
        var validator = new EfDeviceCredentialValidator(db, TimeProvider.System);

        Assert.NotNull(rotated);
        Assert.Equal(enrollment.DeviceId, rotated.DeviceId);
        Assert.NotEqual(enrollment.CredentialId, rotated.CredentialId);
        Assert.False(validator.Validate(enrollment.OrganizationId, enrollment.BranchId, enrollment.DeviceId, enrollment.CredentialSecret));
        Assert.True(validator.Validate(rotated.OrganizationId, rotated.BranchId, rotated.DeviceId, rotated.CredentialSecret));
    }

    [Fact]
    public async Task RevokeAsync_InvalidatesCredential()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var enrollment = await TestDeviceEnrollment.EnrollDeviceAsync(client, TestIds.OrganizationId, TestIds.BranchId);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var lifecycle = new EfDeviceCredentialLifecycleService(db, TimeProvider.System);
        var revoked = await lifecycle.RevokeAsync(enrollment.DeviceId, enrollment.CredentialId, CancellationToken.None);
        var validator = new EfDeviceCredentialValidator(db, TimeProvider.System);

        Assert.NotNull(revoked);
        Assert.Equal(enrollment.CredentialId, revoked.CredentialId);
        Assert.False(validator.Validate(enrollment.OrganizationId, enrollment.BranchId, enrollment.DeviceId, enrollment.CredentialSecret));
    }
}
