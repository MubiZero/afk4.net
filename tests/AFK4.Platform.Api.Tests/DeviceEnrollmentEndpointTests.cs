using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Tests;

public sealed class DeviceEnrollmentEndpointTests
{
    [Fact]
    public async Task PostDeviceEnrollmentCode_ThenEnroll_ReturnsDeviceCredentials()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var organizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
        var branchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);

        var codeResponse = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{branchId}/device-enrollment-codes",
            new CreateDeviceEnrollmentCodeRequest(organizationId, ExpiresInSeconds: 300));
        var code = await codeResponse.Content.ReadFromJsonAsync<DeviceEnrollmentCodeDto>();

        Assert.Equal(HttpStatusCode.OK, codeResponse.StatusCode);
        Assert.NotNull(code);
        Assert.Equal(branchId, code.BranchId);
        Assert.False(string.IsNullOrWhiteSpace(code.Code));

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

        Assert.Equal(HttpStatusCode.OK, enrollmentResponse.StatusCode);
        Assert.NotNull(enrollment);
        Assert.Equal(organizationId, enrollment.OrganizationId);
        Assert.Equal(branchId, enrollment.BranchId);
        Assert.NotEqual(Guid.Empty, enrollment.DeviceId);
        Assert.NotEqual(Guid.Empty, enrollment.CredentialId);
        Assert.False(string.IsNullOrWhiteSpace(enrollment.CredentialSecret));
    }

    [Fact]
    public async Task PostDeviceEnrollment_RejectsReusedEnrollmentCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var organizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
        var branchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var code = await CreateEnrollmentCodeAsync(client, organizationId, branchId);
        var request = new DeviceEnrollmentRequest(
            OrganizationId: organizationId,
            BranchId: branchId,
            EnrollmentCode: code,
            MachineName: "PC-001",
            AgentVersion: "0.1.0",
            ShellVersion: "0.1.0",
            RequestedAtUtc: DateTimeOffset.Parse("2026-05-12T00:01:00Z"));

        var first = await client.PostAsJsonAsync("/api/devices/enroll", request);
        var second = await client.PostAsJsonAsync("/api/devices/enroll", request);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    private static async Task<string> CreateEnrollmentCodeAsync(HttpClient client, Guid organizationId, Guid branchId)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{branchId}/device-enrollment-codes",
            new CreateDeviceEnrollmentCodeRequest(organizationId, ExpiresInSeconds: 300));
        var code = await response.Content.ReadFromJsonAsync<DeviceEnrollmentCodeDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(code);

        return code.Code;
    }
}
