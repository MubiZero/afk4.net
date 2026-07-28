using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Tests;

public sealed class DeviceHeartbeatEndpointTests
{
    [Fact]
    public async Task DeviceHeartbeat_ReturnsServerTimeAndInterval()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var organizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
        var branchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client, organizationId, branchId);
        var request = new DeviceHeartbeatRequest(
            OrganizationId: organizationId,
            BranchId: branchId,
            DeviceId: enrollment.DeviceId,
            MachineName: "PC-001",
            AgentVersion: "0.1.0",
            ShellVersion: "0.1.0",
            ObservedAtUtc: DateTimeOffset.UtcNow,
            IsLocked: true,
            ActiveSessionId: null,
            ActiveSessionLeaseExpiresAtUtc: null,
            ActiveSessionLeaseSequence: null);

        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/devices/{enrollment.DeviceId}/heartbeat")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, enrollment.CredentialSecret);
        var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DeviceHeartbeatResponse>();
        Assert.NotNull(body);
        Assert.Equal(10, body.HeartbeatIntervalSeconds);
        Assert.Empty(body.Commands);
    }

    [Fact]
    public async Task DeviceHeartbeat_ReturnsUnauthorizedWithoutDeviceCredential()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var organizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
        var branchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client, organizationId, branchId);
        var request = new DeviceHeartbeatRequest(
            OrganizationId: organizationId,
            BranchId: branchId,
            DeviceId: enrollment.DeviceId,
            MachineName: "PC-001",
            AgentVersion: "0.1.0",
            ShellVersion: "0.1.0",
            ObservedAtUtc: DateTimeOffset.UtcNow,
            IsLocked: true,
            ActiveSessionId: null,
            ActiveSessionLeaseExpiresAtUtc: null,
            ActiveSessionLeaseSequence: null);

        var response = await client.PostAsJsonAsync($"/api/devices/{enrollment.DeviceId}/heartbeat", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<DeviceEnrollmentResponse> EnrollDeviceAsync(
        HttpClient client,
        Guid organizationId,
        Guid branchId)
    {
        var codeResponse = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{branchId}/device-enrollment-codes",
            new CreateDeviceEnrollmentCodeRequest(organizationId, ExpiresInSeconds: 300));
        var code = await codeResponse.Content.ReadFromJsonAsync<DeviceEnrollmentCodeDto>();
        Assert.NotNull(code);

        var enrollmentResponse = await client.PostAsJsonAsync(
            "/api/devices/enroll",
            new DeviceEnrollmentRequest(
                OrganizationId: organizationId,
                BranchId: branchId,
                EnrollmentCode: code.Code,
                MachineName: "PC-001",
                AgentVersion: "0.1.0",
                ShellVersion: "0.1.0",
                RequestedAtUtc: DateTimeOffset.Parse("2026-05-12T00:01:00Z")));
        var enrollment = await enrollmentResponse.Content.ReadFromJsonAsync<DeviceEnrollmentResponse>();
        Assert.NotNull(enrollment);

        return enrollment;
    }
}
