using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Devices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class DeviceCredentialLifecycleEndpointTests
{
    [Fact]
    public async Task PostDeviceCredentialRotation_WithoutStaffToken_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/devices/{TestIds.DeviceId}/credentials/rotate",
            content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostDeviceCredentialRotation_WithTechnicianPermission_RotatesCredentialAndWritesAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(factory);

        var response = await client.PostAsync(
            $"/api/devices/{enrollment.DeviceId}/credentials/rotate",
            content: null);
        var rotated = await response.Content.ReadFromJsonAsync<RotateDeviceCredentialResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(rotated);
        Assert.Equal(enrollment.DeviceId, rotated.DeviceId);
        Assert.NotEqual(enrollment.CredentialId, rotated.CredentialId);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var validator = new EfDeviceEnrollmentService(dbContext, TimeProvider.System);
        Assert.False(validator.Validate(enrollment.OrganizationId, enrollment.BranchId, enrollment.DeviceId, enrollment.CredentialSecret));
        Assert.True(validator.Validate(rotated.OrganizationId, rotated.BranchId, rotated.DeviceId, rotated.CredentialSecret));

        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.RotateDeviceCredential, audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(TestIds.TechnicianStaffUserId, audit.ActorStaffUserId);
        Assert.Equal(rotated.CredentialId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task PostDeviceCredentialRotation_WithCashierRole_ReturnsForbiddenAndWritesDeniedAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        var enrollment = await EnrollDeviceAsync(factory);

        var response = await client.PostAsync(
            $"/api/devices/{enrollment.DeviceId}/credentials/rotate",
            content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.RotateDeviceCredential, audit.Action);
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
        Assert.Equal(TestIds.TechnicianStaffUserId, audit.ActorStaffUserId);
        Assert.Equal(enrollment.DeviceId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task PostDeviceCredentialRevocation_WithTechnicianPermission_RevokesCredentialAndWritesAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(factory);

        var response = await client.PostAsync(
            $"/api/devices/{enrollment.DeviceId}/credentials/{enrollment.CredentialId}/revoke",
            content: null);
        var revoked = await response.Content.ReadFromJsonAsync<RevokeDeviceCredentialResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(revoked);
        Assert.Equal(enrollment.CredentialId, revoked.CredentialId);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var validator = new EfDeviceEnrollmentService(dbContext, TimeProvider.System);
        Assert.False(validator.Validate(enrollment.OrganizationId, enrollment.BranchId, enrollment.DeviceId, enrollment.CredentialSecret));

        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.RevokeDeviceCredential, audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(TestIds.TechnicianStaffUserId, audit.ActorStaffUserId);
        Assert.Equal(enrollment.CredentialId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task PostDeviceCredentialRevocation_WithCashierRole_ReturnsForbiddenAndWritesDeniedAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        var enrollment = await EnrollDeviceAsync(factory);

        var response = await client.PostAsync(
            $"/api/devices/{enrollment.DeviceId}/credentials/{enrollment.CredentialId}/revoke",
            content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.RevokeDeviceCredential, audit.Action);
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
        Assert.Equal(TestIds.TechnicianStaffUserId, audit.ActorStaffUserId);
        Assert.Equal(enrollment.CredentialId.ToString("D"), audit.TargetId);
    }

    private static async Task<DeviceEnrollmentResponse> EnrollDeviceAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var enrollmentService = new EfDeviceEnrollmentService(dbContext, TimeProvider.System);
        var code = await enrollmentService.CreateEnrollmentCodeAsync(
            TestIds.BranchId,
            new CreateDeviceEnrollmentCodeRequest(TestIds.OrganizationId, ExpiresInSeconds: 300),
            CancellationToken.None);
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
}
