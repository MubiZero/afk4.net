using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Platform.Updates;
using AFK4.Shared.Contracts.Updates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class UpdateEndpointTests
{
    [Theory]
    [InlineData("packages", HttpStatusCode.NotFound)]
    [InlineData("packages/aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa/state", HttpStatusCode.NotFound)]
    [InlineData("rollouts", HttpStatusCode.MethodNotAllowed)]
    [InlineData("rollouts/bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb/state", HttpStatusCode.NotFound)]
    public async Task OrganizationUpdateMutationRoute_IsNotExposed(string suffix, HttpStatusCode expectedStatus)
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/updates/{suffix}",
            new { });

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Fact]
    public async Task GetUpdateRollouts_WithTechnicianPermission_ReturnsPlatformManagedStatus()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);
        var release = await SeedValidatedRolloutAsync(factory);
        await ReportStatusAsync(client, enrollment, release);

        var response = await client.GetAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/updates/rollouts");
        var rollouts = await response.Content.ReadFromJsonAsync<IReadOnlyList<UpdateRolloutStatusDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rollout = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<UpdateRolloutStatusDto>>(rollouts));
        Assert.Equal(release.RolloutId, rollout.UpdateRolloutId);
        Assert.Equal(UpdateRolloutStateNames.Active, rollout.State);
        var status = Assert.Single(rollout.DeviceStatuses);
        Assert.Equal(enrollment.DeviceId, status.DeviceId);
        Assert.Equal(UpdateStatusNames.Installing, status.Status);
    }

    [Fact]
    public async Task PostDeviceUpdateCheck_WithValidCredential_ReturnsValidatedPlatformUpdate()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);
        var release = await SeedValidatedRolloutAsync(factory);
        var request = new DeviceUpdateCheckRequest(
            enrollment.OrganizationId, enrollment.BranchId, enrollment.DeviceId, UpdateChannelNames.Beta,
            DateTimeOffset.Parse("2026-07-29T14:05:00Z"),
            [new DeviceComponentVersionDto(UpdateComponentNames.AgentService, "1.2.2")]);
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/devices/{enrollment.DeviceId}/updates/check")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, enrollment.CredentialSecret);

        var response = await client.SendAsync(message);
        var body = await response.Content.ReadFromJsonAsync<DeviceUpdateCheckResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var update = Assert.Single(Assert.IsType<DeviceUpdateCheckResponse>(body).Updates);
        Assert.Equal(release.PackageId, update.UpdatePackageId);
        Assert.Equal("1.2.3", update.Version);
    }

    [Fact]
    public async Task PostDeviceUpdateCheck_WithoutCredential_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var request = new DeviceUpdateCheckRequest(
            TestIds.OrganizationId, TestIds.BranchId, TestIds.DeviceId, UpdateChannelNames.Beta,
            DateTimeOffset.Parse("2026-07-29T14:05:00Z"), []);

        var response = await client.PostAsJsonAsync($"/api/devices/{TestIds.DeviceId}/updates/check", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task ReportStatusAsync(
        HttpClient client,
        DeviceEnrollmentResponse enrollment,
        (Guid PackageId, Guid RolloutId) release)
    {
        var request = new DeviceUpdateStatusReportRequest(
            enrollment.OrganizationId, enrollment.BranchId, enrollment.DeviceId,
            release.RolloutId, release.PackageId, UpdateComponentNames.AgentService,
            "1.2.2", "1.2.3", UpdateStatusNames.Installing, "install started",
            DateTimeOffset.Parse("2026-07-29T14:06:00Z"));
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/devices/{enrollment.DeviceId}/updates/status")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, enrollment.CredentialSecret);
        var response = await client.SendAsync(message);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<(Guid PackageId, Guid RolloutId)> SeedValidatedRolloutAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var packageId = Guid.NewGuid();
        var rolloutId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-07-29T14:00:00Z");
        db.UpdatePackages.Add(new UpdatePackageEntity
        {
            UpdatePackageId = packageId,
            Component = UpdateComponentNames.AgentService,
            Version = "1.2.3",
            Channel = UpdateChannelNames.Beta,
            ArtifactUri = "https://updates.afk4.test/agent/1.2.3/agent.msi",
            Sha256 = new string('a', 64),
            Signature = "signature",
            SignatureAlgorithm = UpdatePackageSignatureAlgorithmNames.EcdsaP256Sha256IeeeP1363,
            SizeBytes = 42_000_000,
            State = UpdatePackageStateNames.Validated,
            ReleaseNotes = "Agent update.",
            CreatedByPlatformAdminUserId = actorId,
            ValidatedByPlatformAdminUserId = actorId,
            ValidatedAtUtc = now,
            CreatedAtUtc = now
        });
        db.UpdateRollouts.Add(new UpdateRolloutEntity
        {
            UpdateRolloutId = rolloutId,
            UpdatePackageId = packageId,
            Component = UpdateComponentNames.AgentService,
            Version = "1.2.3",
            Channel = UpdateChannelNames.Beta,
            State = UpdateRolloutStateNames.Active,
            TargetKind = PlatformUpdateTargetKindNames.Branch,
            BatchPercent = 100,
            Reason = "Platform-managed rollout.",
            CreatedByPlatformAdminUserId = actorId,
            CreatedAtUtc = now,
            StartsAtUtc = now
        });
        db.UpdateRolloutTargets.Add(new UpdateRolloutTargetEntity
        {
            UpdateRolloutTargetId = Guid.NewGuid(),
            UpdateRolloutId = rolloutId,
            TargetKind = PlatformUpdateTargetKindNames.Branch,
            BranchId = TestIds.BranchId,
            CreatedAtUtc = now
        });
        await db.SaveChangesAsync();
        return (packageId, rolloutId);
    }

    private static async Task<DeviceEnrollmentResponse> EnrollDeviceAsync(HttpClient client)
    {
        var codeResponse = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/device-enrollment-codes",
            new CreateDeviceEnrollmentCodeRequest(TestIds.OrganizationId, 300));
        var code = await codeResponse.Content.ReadFromJsonAsync<DeviceEnrollmentCodeDto>();
        Assert.Equal(HttpStatusCode.OK, codeResponse.StatusCode);
        Assert.NotNull(code);

        var enrollmentResponse = await client.PostAsJsonAsync(
            "/api/devices/enroll",
            new DeviceEnrollmentRequest(
                TestIds.OrganizationId, TestIds.BranchId, code.Code, "PC-001", "1.2.2", "1.2.2",
                DateTimeOffset.Parse("2026-07-29T13:55:00Z")));
        var enrollment = await enrollmentResponse.Content.ReadFromJsonAsync<DeviceEnrollmentResponse>();
        Assert.Equal(HttpStatusCode.OK, enrollmentResponse.StatusCode);
        Assert.NotNull(enrollment);
        return enrollment;
    }
}
