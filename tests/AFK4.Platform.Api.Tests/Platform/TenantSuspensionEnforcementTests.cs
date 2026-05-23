using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Platform.Tenants;
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
