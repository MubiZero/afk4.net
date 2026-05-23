using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Platform.Tenants;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Updates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class TenantSuspensionEnforcementTests
{
    [Fact]
    public async Task StaffMutation_OnSuspendedTenant_Returns403TenantSuspended()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);

        await SetTenantStatusAsync(factory, TenantStatusNames.Suspended, "Unpaid invoice");

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/device-enrollment-codes",
            new CreateDeviceEnrollmentCodeRequest(TestIds.OrganizationId, ExpiresInSeconds: 3600));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(document);
        Assert.Equal("TenantSuspended", document.RootElement.GetProperty("error").GetString());
        Assert.Equal(TenantStatusNames.Suspended, document.RootElement.GetProperty("status").GetString());
        Assert.Equal("Unpaid invoice", document.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task StaffRead_OnSuspendedTenant_StillReturns200()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);

        await SetTenantStatusAsync(factory, TenantStatusNames.Suspended, "Holding");

        var response = await client.GetAsync($"/api/branches/{TestIds.BranchId:D}/floor-map");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StaffSignIn_OnSuspendedTenant_StillReturnsTokens()
    {
        await using var factory = new PlatformApiFactory();
        using (var seedClient = factory.CreateClient())
        {
            await StaffAuthTestHelper.AuthorizeAsAsync(factory, seedClient, StaffRoleNames.Owner);
        }

        await SetTenantStatusAsync(factory, TenantStatusNames.Suspended, "Holding");

        using var freshClient = factory.CreateClient();
        var signInResponse = await freshClient.PostAsJsonAsync(
            "/api/auth/staff/sign-in",
            new StaffSignInRequest(TestIds.OrganizationId, "tech@afk4.test", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.OK, signInResponse.StatusCode);
    }

    [Fact]
    public async Task StaffMutation_OnDeletionPendingTenant_AlsoReturns403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);

        await SetTenantStatusAsync(factory, TenantStatusNames.DeletionPending, "Tenant offboarding");

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/device-enrollment-codes",
            new CreateDeviceEnrollmentCodeRequest(TestIds.OrganizationId, ExpiresInSeconds: 3600));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(document);
        Assert.Equal(TenantStatusNames.DeletionPending, document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task StaffMutation_OnActiveTenant_StillSucceeds()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/device-enrollment-codes",
            new CreateDeviceEnrollmentCodeRequest(TestIds.OrganizationId, ExpiresInSeconds: 3600));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PlatformAdminPatch_OnSuspendedTenant_StillAllowed()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            dbContext.Organizations.Add(new OrganizationEntity
            {
                OrganizationId = TestIds.OrganizationId,
                Slug = "demo-org",
                Name = "Demo Org",
                Status = TenantStatusNames.Suspended,
                StatusReason = "Frozen for payment",
                StatusChangedAtUtc = DateTimeOffset.UtcNow,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/tenants/{TestIds.OrganizationId:D}/status",
            new UpdateTenantStatusRequest(TenantStatusNames.Active, ""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StaffMutation_AfterReactivation_SucceedsAgain()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);

        await SetTenantStatusAsync(factory, TenantStatusNames.Suspended, "Pause");

        var blocked = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/device-enrollment-codes",
            new CreateDeviceEnrollmentCodeRequest(TestIds.OrganizationId, ExpiresInSeconds: 3600));
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);

        await SetTenantStatusAsync(factory, TenantStatusNames.Active, null);

        var allowed = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/device-enrollment-codes",
            new CreateDeviceEnrollmentCodeRequest(TestIds.OrganizationId, ExpiresInSeconds: 3600));
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task DeviceHeartbeat_OnSuspendedTenant_Returns403TenantSuspended()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);

        await SetTenantStatusAsync(factory, TenantStatusNames.Suspended, "Unpaid invoice");

        using var message = BuildHeartbeatRequest(enrollment);
        var response = await client.SendAsync(message);

        await AssertTenantSuspendedAsync(response, TenantStatusNames.Suspended, "Unpaid invoice");
    }

    [Fact]
    public async Task DeviceHeartbeat_OnDeletionPendingTenant_AlsoReturns403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);

        await SetTenantStatusAsync(factory, TenantStatusNames.DeletionPending, "Offboarding");

        using var message = BuildHeartbeatRequest(enrollment);
        var response = await client.SendAsync(message);

        await AssertTenantSuspendedAsync(response, TenantStatusNames.DeletionPending, "Offboarding");
    }

    [Fact]
    public async Task DeviceHeartbeat_OnActiveTenant_StillSucceeds()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);

        using var message = BuildHeartbeatRequest(enrollment);
        var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeviceSessionReconciliation_OnSuspendedTenant_Returns403TenantSuspended()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);

        await SetTenantStatusAsync(factory, TenantStatusNames.Suspended, "Suspended");

        var request = new DeviceSessionSnapshotRequest(
            OrganizationId: TestIds.OrganizationId,
            BranchId: TestIds.BranchId,
            DeviceId: enrollment.DeviceId,
            ActiveSessionId: null,
            ActiveLease: null,
            IsLocked: true,
            PendingLocalEventCount: 0,
            ObservedAtUtc: DateTimeOffset.UtcNow);

        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/devices/{enrollment.DeviceId}/session-reconciliation")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, enrollment.CredentialSecret);
        var response = await client.SendAsync(message);

        await AssertTenantSuspendedAsync(response, TenantStatusNames.Suspended, "Suspended");
    }

    [Fact]
    public async Task DeviceEnrollment_OnSuspendedTenant_Returns403TenantSuspended()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);

        // Create an enrollment code while still active, so the test isolates the suspension check
        // from the "no enrollment code" failure path.
        var codeResponse = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId}/device-enrollment-codes",
            new CreateDeviceEnrollmentCodeRequest(TestIds.OrganizationId, ExpiresInSeconds: 600));
        var code = await codeResponse.Content.ReadFromJsonAsync<DeviceEnrollmentCodeDto>();
        Assert.NotNull(code);

        await SetTenantStatusAsync(factory, TenantStatusNames.Suspended, "Frozen");

        var enrollResponse = await client.PostAsJsonAsync(
            "/api/devices/enroll",
            new DeviceEnrollmentRequest(
                OrganizationId: TestIds.OrganizationId,
                BranchId: TestIds.BranchId,
                EnrollmentCode: code.Code,
                MachineName: "PC-suspended",
                AgentVersion: "0.1.0",
                ShellVersion: "0.1.0",
                RequestedAtUtc: DateTimeOffset.UtcNow));

        await AssertTenantSuspendedAsync(enrollResponse, TenantStatusNames.Suspended, "Frozen");
    }

    [Fact]
    public async Task DeviceUpdatesCheck_OnSuspendedTenant_Returns403TenantSuspended()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);

        await SetTenantStatusAsync(factory, TenantStatusNames.Suspended, "Paused");

        var request = new DeviceUpdateCheckRequest(
            OrganizationId: TestIds.OrganizationId,
            BranchId: TestIds.BranchId,
            DeviceId: enrollment.DeviceId,
            Channel: "internal",
            CheckedAtUtc: DateTimeOffset.UtcNow,
            InstalledComponents: Array.Empty<DeviceComponentVersionDto>());

        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/devices/{enrollment.DeviceId}/updates/check")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, enrollment.CredentialSecret);
        var response = await client.SendAsync(message);

        await AssertTenantSuspendedAsync(response, TenantStatusNames.Suspended, "Paused");
    }

    [Fact]
    public async Task DeviceUpdatesStatus_OnSuspendedTenant_Returns403TenantSuspended()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);

        await SetTenantStatusAsync(factory, TenantStatusNames.Suspended, "Paused");

        var request = new DeviceUpdateStatusReportRequest(
            OrganizationId: TestIds.OrganizationId,
            BranchId: TestIds.BranchId,
            DeviceId: enrollment.DeviceId,
            UpdateRolloutId: Guid.NewGuid(),
            UpdatePackageId: Guid.NewGuid(),
            Component: "agent-service",
            InstalledVersion: "0.1.0",
            TargetVersion: "0.2.0",
            Status: "installing",
            Message: "",
            ObservedAtUtc: DateTimeOffset.UtcNow);

        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/devices/{enrollment.DeviceId}/updates/status")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, enrollment.CredentialSecret);
        var response = await client.SendAsync(message);

        await AssertTenantSuspendedAsync(response, TenantStatusNames.Suspended, "Paused");
    }

    private static HttpRequestMessage BuildHeartbeatRequest(DeviceEnrollmentResponse enrollment)
    {
        var request = new DeviceHeartbeatRequest(
            OrganizationId: TestIds.OrganizationId,
            BranchId: TestIds.BranchId,
            DeviceId: enrollment.DeviceId,
            MachineName: "PC-suspended",
            AgentVersion: "0.1.0",
            ShellVersion: "0.1.0",
            ObservedAtUtc: DateTimeOffset.UtcNow,
            IsLocked: true,
            ActiveSessionId: null,
            ActiveSessionLeaseExpiresAtUtc: null,
            ActiveSessionLeaseSequence: null);

        var message = new HttpRequestMessage(HttpMethod.Post, $"/api/devices/{enrollment.DeviceId}/heartbeat")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, enrollment.CredentialSecret);
        return message;
    }

    private static async Task<DeviceEnrollmentResponse> EnrollDeviceAsync(HttpClient client)
    {
        var codeResponse = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId}/device-enrollment-codes",
            new CreateDeviceEnrollmentCodeRequest(TestIds.OrganizationId, ExpiresInSeconds: 300));
        var code = await codeResponse.Content.ReadFromJsonAsync<DeviceEnrollmentCodeDto>();
        Assert.NotNull(code);

        var enrollmentResponse = await client.PostAsJsonAsync(
            "/api/devices/enroll",
            new DeviceEnrollmentRequest(
                OrganizationId: TestIds.OrganizationId,
                BranchId: TestIds.BranchId,
                EnrollmentCode: code.Code,
                MachineName: "PC-001",
                AgentVersion: "0.1.0",
                ShellVersion: "0.1.0",
                RequestedAtUtc: DateTimeOffset.UtcNow));
        var enrollment = await enrollmentResponse.Content.ReadFromJsonAsync<DeviceEnrollmentResponse>();
        Assert.NotNull(enrollment);
        return enrollment;
    }

    private static async Task AssertTenantSuspendedAsync(HttpResponseMessage response, string expectedStatus, string expectedReason)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(document);
        Assert.Equal("TenantSuspended", document.RootElement.GetProperty("error").GetString());
        Assert.Equal(expectedStatus, document.RootElement.GetProperty("status").GetString());
        Assert.Equal(expectedReason, document.RootElement.GetProperty("reason").GetString());
    }

    private static async Task SetTenantStatusAsync(
        PlatformApiFactory factory,
        string status,
        string? reason)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = await dbContext.Organizations.SingleAsync(org => org.OrganizationId == TestIds.OrganizationId);
        organization.Status = status;
        organization.StatusReason = reason;
        organization.StatusChangedAtUtc = DateTimeOffset.UtcNow;
        organization.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();
    }
}
