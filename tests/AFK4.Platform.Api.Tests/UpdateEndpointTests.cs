using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Updates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class UpdateEndpointTests
{
    [Fact]
    public async Task PostUpdatePackage_WithoutStaffToken_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId}/updates/packages",
            PackageRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostUpdatePackage_WithTechnicianPermission_CreatesPackageAndAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId}/updates/packages",
            PackageRequest());
        var package = await response.Content.ReadFromJsonAsync<UpdatePackageDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(package);
        Assert.Equal(UpdateComponentNames.AgentService, package.Component);
        Assert.Equal(UpdatePackageStateNames.Registered, package.State);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.RegisterUpdatePackage, audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(package.UpdatePackageId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task PostUpdatePackage_WithCashierRole_ReturnsForbiddenAndWritesDeniedAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId}/updates/packages",
            PackageRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.RegisterUpdatePackage, audit.Action);
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
    }

    [Fact]
    public async Task PostUpdateRollout_WithTechnicianPermission_CreatesRolloutAndCanReadIt()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        var package = await RegisterPackageAsync(client);

        var createResponse = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId}/updates/rollouts",
            new CreateUpdateRolloutRequest(
                TestIds.OrganizationId,
                package.UpdatePackageId,
                UpdateChannelNames.Beta,
                UpdateTargetKindNames.Branch,
                [],
                BatchPercent: 100,
                StartsAtUtc: DateTimeOffset.Parse("2026-05-14T14:00:00Z"),
                Reason: "Branch rollout."));
        var rollout = await createResponse.Content.ReadFromJsonAsync<UpdateRolloutDto>();

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(rollout);
        Assert.Equal(UpdateRolloutStateNames.Active, rollout.State);

        var getResponse = await client.GetAsync($"/api/branches/{TestIds.BranchId}/updates/rollouts/{rollout.UpdateRolloutId}");
        var readBack = await getResponse.Content.ReadFromJsonAsync<UpdateRolloutDto>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(readBack);
        Assert.Equal(rollout.UpdateRolloutId, readBack.UpdateRolloutId);
    }

    [Fact]
    public async Task PostDeviceUpdateCheck_WithValidDeviceCredential_ReturnsAvailableUpdate()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);
        var package = await RegisterPackageAsync(client);
        await CreateBranchRolloutAsync(client, package.UpdatePackageId);
        var request = new DeviceUpdateCheckRequest(
            enrollment.OrganizationId,
            enrollment.BranchId,
            enrollment.DeviceId,
            UpdateChannelNames.Beta,
            CheckedAtUtc: DateTimeOffset.Parse("2026-05-14T14:05:00Z"),
            InstalledComponents: [new DeviceComponentVersionDto(UpdateComponentNames.AgentService, "1.2.2")]);

        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/devices/{enrollment.DeviceId}/updates/check")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, enrollment.CredentialSecret);
        var response = await client.SendAsync(message);
        var body = await response.Content.ReadFromJsonAsync<DeviceUpdateCheckResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        var update = Assert.Single(body.Updates);
        Assert.Equal(package.UpdatePackageId, update.UpdatePackageId);
        Assert.Equal("1.2.3", update.Version);
    }

    [Fact]
    public async Task PostDeviceUpdateCheck_WithoutCredential_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var request = new DeviceUpdateCheckRequest(
            TestIds.OrganizationId,
            TestIds.BranchId,
            TestIds.DeviceId,
            UpdateChannelNames.Beta,
            CheckedAtUtc: DateTimeOffset.Parse("2026-05-14T14:05:00Z"),
            InstalledComponents: []);

        var response = await client.PostAsJsonAsync($"/api/devices/{TestIds.DeviceId}/updates/check", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostDeviceUpdateStatus_WithValidDeviceCredential_RecordsStatus()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);
        var package = await RegisterPackageAsync(client);
        var rollout = await CreateBranchRolloutAsync(client, package.UpdatePackageId);
        var request = new DeviceUpdateStatusReportRequest(
            enrollment.OrganizationId,
            enrollment.BranchId,
            enrollment.DeviceId,
            rollout.UpdateRolloutId,
            package.UpdatePackageId,
            UpdateComponentNames.AgentService,
            InstalledVersion: "1.2.2",
            TargetVersion: "1.2.3",
            UpdateStatusNames.Installing,
            Message: "install started",
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-14T14:06:00Z"));

        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/devices/{enrollment.DeviceId}/updates/status")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, enrollment.CredentialSecret);
        var response = await client.SendAsync(message);
        var body = await response.Content.ReadFromJsonAsync<DeviceUpdateStatusResultDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(UpdateStatusNames.Installing, body.Status);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var stored = await dbContext.DeviceUpdateStatuses.SingleAsync();
        Assert.Equal(enrollment.DeviceId, stored.DeviceId);
        Assert.Equal(UpdateStatusNames.Installing, stored.Status);
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

    private static async Task<UpdatePackageDto> RegisterPackageAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId}/updates/packages",
            PackageRequest());
        var package = await response.Content.ReadFromJsonAsync<UpdatePackageDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(package);

        return package;
    }

    private static async Task<UpdateRolloutDto> CreateBranchRolloutAsync(HttpClient client, Guid packageId)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId}/updates/rollouts",
            new CreateUpdateRolloutRequest(
                TestIds.OrganizationId,
                packageId,
                UpdateChannelNames.Beta,
                UpdateTargetKindNames.Branch,
                [],
                BatchPercent: 100,
                StartsAtUtc: DateTimeOffset.Parse("2026-05-14T14:00:00Z"),
                Reason: "Branch rollout."));
        var rollout = await response.Content.ReadFromJsonAsync<UpdateRolloutDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(rollout);

        return rollout;
    }

    private static async Task<DeviceEnrollmentResponse> EnrollDeviceAsync(HttpClient client)
    {
        var codeResponse = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId}/device-enrollment-codes",
            new CreateDeviceEnrollmentCodeRequest(TestIds.OrganizationId, ExpiresInSeconds: 300));
        var code = await codeResponse.Content.ReadFromJsonAsync<DeviceEnrollmentCodeDto>();
        Assert.Equal(HttpStatusCode.OK, codeResponse.StatusCode);
        Assert.NotNull(code);

        var enrollmentResponse = await client.PostAsJsonAsync(
            "/api/devices/enroll",
            new DeviceEnrollmentRequest(
                TestIds.OrganizationId,
                TestIds.BranchId,
                code.Code,
                MachineName: "PC-001",
                AgentVersion: "1.2.2",
                ShellVersion: "1.2.2",
                RequestedAtUtc: DateTimeOffset.Parse("2026-05-14T13:55:00Z")));
        var enrollment = await enrollmentResponse.Content.ReadFromJsonAsync<DeviceEnrollmentResponse>();
        Assert.Equal(HttpStatusCode.OK, enrollmentResponse.StatusCode);
        Assert.NotNull(enrollment);

        return enrollment;
    }
}
