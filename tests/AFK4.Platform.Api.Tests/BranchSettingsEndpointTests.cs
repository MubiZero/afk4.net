using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class BranchSettingsEndpointTests
{
    [Fact]
    public async Task Get_WithoutStaffToken_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/branches/{TestIds.BranchId:D}/settings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithStaffWithoutPermission_ReturnsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);

        var response = await client.GetAsync($"/api/branches/{TestIds.BranchId:D}/settings");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithOwner_ReturnsDefaultsToFalse()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);

        var response = await client.GetAsync($"/api/branches/{TestIds.BranchId:D}/settings");
        var settings = await response.Content.ReadFromJsonAsync<BranchSettingsDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(settings);
        Assert.Equal(TestIds.BranchId, settings.BranchId);
        Assert.False(settings.RequireManualDeviceApproval);
    }

    [Fact]
    public async Task Put_TogglesManualApprovalAndPersists()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);

        var request = new UpdateBranchSettingsRequest(TestIds.OrganizationId, RequireManualDeviceApproval: true);
        var response = await client.PutAsJsonAsync($"/api/branches/{TestIds.BranchId:D}/settings", request);
        var settings = await response.Content.ReadFromJsonAsync<BranchSettingsDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(settings);
        Assert.True(settings.RequireManualDeviceApproval);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var persisted = await dbContext.Branches
            .AsNoTracking()
            .SingleAsync(b => b.BranchId == TestIds.BranchId);
        Assert.True(persisted.RequireManualDeviceApproval);
    }

    [Fact]
    public async Task Put_WithMismatchedOrganization_ReturnsBadRequest()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);

        var request = new UpdateBranchSettingsRequest(Guid.NewGuid(), RequireManualDeviceApproval: true);
        var response = await client.PutAsJsonAsync($"/api/branches/{TestIds.BranchId:D}/settings", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
