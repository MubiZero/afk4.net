using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Tests.Devices;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Platform.Organizations;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Updates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class OrganizationSuspensionEnforcementTests
{
    [Fact]
    public async Task StaffMutation_OnSuspendedOrganization_Returns403OrganizationSuspended()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        await SetOrganizationStatusAsync(factory, OrganizationStatusNames.Suspended, "Unpaid invoice");

        var response = await client.PostAsJsonAsync(
            InstallCodeRoutes.Branch(TestIds.OrganizationId, TestIds.BranchId),
            new CreateInstallCodeRequest(LifetimeHours: 24, MaxDevices: 10));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(document);
        Assert.Equal("OrganizationSuspended", document.RootElement.GetProperty("error").GetString());
        Assert.Equal(OrganizationStatusNames.Suspended, document.RootElement.GetProperty("status").GetString());
        Assert.Equal("Unpaid invoice", document.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task StaffRead_OnSuspendedOrganization_StillReturns200()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        await SetOrganizationStatusAsync(factory, OrganizationStatusNames.Suspended, "Holding");

        var response = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/floor-map");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StaffSignIn_OnSuspendedOrganization_StillReturnsTokens()
    {
        await using var factory = new PlatformApiFactory();
        using (var seedClient = factory.CreateClient())
        {
            await StaffAuthTestHelper.AuthorizeAsAsync(factory, seedClient, OrganizationRoleNames.OrganizationOwner);
        }

        await SetOrganizationStatusAsync(factory, OrganizationStatusNames.Suspended, "Holding");

        using var freshClient = factory.CreateClient();
        var signInResponse = await freshClient.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/auth/staff/sign-in",
            new StaffSignInRequest(TestIds.OrganizationId, "tech@afk4.test", "246813"));

        Assert.Equal(HttpStatusCode.OK, signInResponse.StatusCode);
    }

    [Fact]
    public async Task StaffMutation_OnDeletionPendingOrganization_AlsoReturns403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        await SetOrganizationStatusAsync(factory, OrganizationStatusNames.DeletionPending, "Organization offboarding");

        var response = await client.PostAsJsonAsync(
            InstallCodeRoutes.Branch(TestIds.OrganizationId, TestIds.BranchId),
            new CreateInstallCodeRequest(LifetimeHours: 24, MaxDevices: 10));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(document);
        Assert.Equal(OrganizationStatusNames.DeletionPending, document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task StaffMutation_OnActiveOrganization_StillSucceeds()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        var response = await client.PostAsJsonAsync(
            InstallCodeRoutes.Branch(TestIds.OrganizationId, TestIds.BranchId),
            new CreateInstallCodeRequest(LifetimeHours: 24, MaxDevices: 10));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PlatformAdminPatch_OnSuspendedOrganization_StillAllowed()
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
                Status = OrganizationStatusNames.Suspended,
                StatusReason = "Frozen for payment",
                StatusChangedAtUtc = DateTimeOffset.UtcNow,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{TestIds.OrganizationId:D}/status",
            new UpdateOrganizationStatusRequest(OrganizationStatusNames.Active, ""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StaffMutation_AfterReactivation_SucceedsAgain()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        await SetOrganizationStatusAsync(factory, OrganizationStatusNames.Suspended, "Pause");

        var blocked = await client.PostAsJsonAsync(
            InstallCodeRoutes.Branch(TestIds.OrganizationId, TestIds.BranchId),
            new CreateInstallCodeRequest(LifetimeHours: 24, MaxDevices: 10));
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);

        await SetOrganizationStatusAsync(factory, OrganizationStatusNames.Active, null);

        var allowed = await client.PostAsJsonAsync(
            InstallCodeRoutes.Branch(TestIds.OrganizationId, TestIds.BranchId),
            new CreateInstallCodeRequest(LifetimeHours: 24, MaxDevices: 10));
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task DeviceHeartbeat_OnSuspendedOrganization_Returns403OrganizationSuspended()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);

        await SetOrganizationStatusAsync(factory, OrganizationStatusNames.Suspended, "Unpaid invoice");

        using var message = BuildHeartbeatRequest(enrollment);
        var response = await client.SendAsync(message);

        await AssertOrganizationSuspendedAsync(response, OrganizationStatusNames.Suspended, "Unpaid invoice");
    }

    [Fact]
    public async Task DeviceHeartbeat_OnDeletionPendingOrganization_AlsoReturns403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);

        await SetOrganizationStatusAsync(factory, OrganizationStatusNames.DeletionPending, "Offboarding");

        using var message = BuildHeartbeatRequest(enrollment);
        var response = await client.SendAsync(message);

        await AssertOrganizationSuspendedAsync(response, OrganizationStatusNames.DeletionPending, "Offboarding");
    }

    [Fact]
    public async Task DeviceHeartbeat_OnActiveOrganization_StillSucceeds()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);

        using var message = BuildHeartbeatRequest(enrollment);
        var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeviceSessionReconciliation_OnSuspendedOrganization_Returns403OrganizationSuspended()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);

        await SetOrganizationStatusAsync(factory, OrganizationStatusNames.Suspended, "Suspended");

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

        await AssertOrganizationSuspendedAsync(response, OrganizationStatusNames.Suspended, "Suspended");
    }

    // ВНИМАНИЕ: тихая установка по коду не проходит через IOrganizationStatusGuard, которым
    // защищены остальные device-эндпойнты (heartbeat, session-reconciliation, updates/*), и не даёт
    // тот же контракт «403 OrganizationSuspended со статусом и причиной». Отказ она всё же
    // возвращает — приостановленную/удаляемую организацию EfInstallService проверяет сам
    // (EnrollResolvedAsync: organization.Status != Active), но обычным BadRequest 400 с общей
    // фразой. Тест фиксирует то, что путь реально делает, а не то, что делают остальные эндпойнты —
    // расхождение форматов ошибок между этим путём и остальными названо владельцу отдельно.
    [Fact]
    public async Task DeviceEnrollment_OnSuspendedOrganization_IsRefusedButNotWithTheSharedContract()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);

        // Код выдаём, пока организация ещё активна, — приостановку проверяем отдельно от «кода нет».
        var codeResponse = await client.PostAsJsonAsync(
            InstallCodeRoutes.Branch(TestIds.OrganizationId, TestIds.BranchId),
            new CreateInstallCodeRequest(LifetimeHours: 24, MaxDevices: 10));
        var code = await codeResponse.Content.ReadFromJsonAsync<InstallCodeDto>();
        Assert.NotNull(code);

        await SetOrganizationStatusAsync(factory, OrganizationStatusNames.Suspended, "Frozen");

        // Настоящий ПК ставится анонимно, без токена сотрудника — тем самым клиентом проверяем и
        // здесь, а не тем, что уже несёт Bearer техника: с ним отказ пришёл бы от общего
        // OrganizationSuspensionMiddleware (он ловит любую мутацию авторизованного сотрудника), а
        // не от самой тихой установки.
        using var deviceClient = factory.CreateClient();
        var enrollResponse = await deviceClient.PostAsJsonAsync(
            InstallRoutes.CodeEnroll,
            new InstallCodeEnrollRequest(
                code!.Code!,
                SeatName: null,
                DisplayName: null,
                MachineName: "PC-suspended",
                DevicePublicKey: $"test-key-{Guid.NewGuid():N}"));

        Assert.Equal(HttpStatusCode.BadRequest, enrollResponse.StatusCode);
        using var document = await enrollResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(document);
        Assert.Equal("Organization is not active.", document.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task DeviceUpdatesCheck_OnSuspendedOrganization_Returns403OrganizationSuspended()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);

        await SetOrganizationStatusAsync(factory, OrganizationStatusNames.Suspended, "Paused");

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

        await AssertOrganizationSuspendedAsync(response, OrganizationStatusNames.Suspended, "Paused");
    }

    [Fact]
    public async Task DeviceUpdatesStatus_OnSuspendedOrganization_Returns403OrganizationSuspended()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);

        await SetOrganizationStatusAsync(factory, OrganizationStatusNames.Suspended, "Paused");

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

        await AssertOrganizationSuspendedAsync(response, OrganizationStatusNames.Suspended, "Paused");
    }

    private static HttpRequestMessage BuildHeartbeatRequest(InstallEnrollResponse enrollment)
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

    private static Task<InstallEnrollResponse> EnrollDeviceAsync(HttpClient client) =>
        TestDeviceEnrollment.EnrollDeviceAsync(client, TestIds.OrganizationId, TestIds.BranchId);

    private static async Task AssertOrganizationSuspendedAsync(HttpResponseMessage response, string expectedStatus, string expectedReason)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(document);
        Assert.Equal("OrganizationSuspended", document.RootElement.GetProperty("error").GetString());
        Assert.Equal(expectedStatus, document.RootElement.GetProperty("status").GetString());
        Assert.Equal(expectedReason, document.RootElement.GetProperty("reason").GetString());
    }

    private static async Task SetOrganizationStatusAsync(
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
