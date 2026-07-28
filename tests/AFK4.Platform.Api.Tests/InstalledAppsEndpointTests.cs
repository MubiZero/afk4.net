using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Devices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class InstalledAppsEndpointTests
{
    [Fact]
    public async Task PostInstalledAppsReport_WithValidDeviceCredential_ReplacesDeviceSnapshotRows()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);
        await SeedExistingInstalledAppAsync(factory, enrollment.DeviceId);
        var request = new InstalledAppReportRequest(
            OrganizationId: enrollment.OrganizationId,
            BranchId: enrollment.BranchId,
            DeviceId: enrollment.DeviceId,
            ReportedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            Apps:
            [
                new InstalledAppDto(
                    DisplayName: "Counter-Strike 2",
                    Version: "2.0.0",
                    Publisher: "Valve",
                    InstallLocation: @"C:\Games\Counter-Strike 2",
                    InstalledAtUtc: DateTimeOffset.Parse("2026-05-01T08:30:00Z")),
                new InstalledAppDto(
                    DisplayName: "Discord",
                    Version: "1.0.9059",
                    Publisher: "Discord Inc.",
                    InstallLocation: null,
                    InstalledAtUtc: null)
            ]);

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/devices/{enrollment.DeviceId}/installed-apps/report")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, enrollment.CredentialSecret);
        var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var apps = await dbContext.DeviceInstalledApps
            .AsNoTracking()
            .Where(app => app.DeviceId == enrollment.DeviceId)
            .OrderBy(app => app.DisplayName)
            .ToListAsync();

        Assert.Collection(
            apps,
            app =>
            {
                Assert.Equal("Counter-Strike 2", app.DisplayName);
                Assert.Equal("2.0.0", app.Version);
                Assert.Equal("Valve", app.Publisher);
                Assert.Equal(@"C:\Games\Counter-Strike 2", app.InstallLocation);
                Assert.Equal(DateTimeOffset.Parse("2026-05-01T08:30:00Z"), app.InstalledAtUtc);
                Assert.Equal(DateTimeOffset.Parse("2026-05-13T10:00:00Z"), app.ReportedAtUtc);
            },
            app =>
            {
                Assert.Equal("Discord", app.DisplayName);
                Assert.Equal("1.0.9059", app.Version);
                Assert.Null(app.InstallLocation);
                Assert.Null(app.InstalledAtUtc);
            });
    }

    [Fact]
    public async Task PostInstalledAppsReport_WithoutDeviceCredential_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);
        var request = CreateReport(enrollment);

        var response = await client.PostAsJsonAsync(
            $"/api/devices/{enrollment.DeviceId}/installed-apps/report",
            request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostInstalledAppsReport_WithRouteDeviceMismatch_ReturnsBadRequest()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);
        var request = CreateReport(enrollment) with { DeviceId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa") };

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/devices/{enrollment.DeviceId}/installed-apps/report")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, enrollment.CredentialSecret);
        var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostInstalledAppsReport_WithCredentialForDifferentDevice_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var firstEnrollment = await EnrollDeviceAsync(client, "PC-001");
        var secondEnrollment = await EnrollDeviceAsync(client, "PC-002");
        var request = CreateReport(firstEnrollment);

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/devices/{firstEnrollment.DeviceId}/installed-apps/report")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, secondEnrollment.CredentialSecret);
        var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static InstalledAppReportRequest CreateReport(DeviceEnrollmentResponse enrollment)
    {
        return new InstalledAppReportRequest(
            OrganizationId: enrollment.OrganizationId,
            BranchId: enrollment.BranchId,
            DeviceId: enrollment.DeviceId,
            ReportedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            Apps:
            [
                new InstalledAppDto(
                    DisplayName: "Discord",
                    Version: "1.0.9059",
                    Publisher: "Discord Inc.",
                    InstallLocation: null,
                    InstalledAtUtc: null)
            ]);
    }

    private static async Task<DeviceEnrollmentResponse> EnrollDeviceAsync(
        HttpClient client,
        string machineName = "PC-001")
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
                OrganizationId: TestIds.OrganizationId,
                BranchId: TestIds.BranchId,
                EnrollmentCode: code.Code,
                MachineName: machineName,
                AgentVersion: "0.1.0",
                ShellVersion: "0.1.0",
                RequestedAtUtc: DateTimeOffset.Parse("2026-05-13T09:55:00Z")));
        var enrollment = await enrollmentResponse.Content.ReadFromJsonAsync<DeviceEnrollmentResponse>();
        Assert.Equal(HttpStatusCode.OK, enrollmentResponse.StatusCode);
        Assert.NotNull(enrollment);

        return enrollment;
    }

    private static async Task SeedExistingInstalledAppAsync(PlatformApiFactory factory, Guid deviceId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        dbContext.DeviceInstalledApps.Add(new DeviceInstalledAppEntity
        {
            DeviceInstalledAppId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            DeviceId = deviceId,
            DisplayName = "Old App",
            Version = "0.0.1",
            Publisher = "Legacy",
            InstallLocation = null,
            InstalledAtUtc = null,
            ReportedAtUtc = DateTimeOffset.Parse("2026-05-13T09:00:00Z")
        });
        await dbContext.SaveChangesAsync();
    }
}
