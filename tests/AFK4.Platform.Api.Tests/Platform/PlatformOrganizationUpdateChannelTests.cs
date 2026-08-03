using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformOrganizationUpdateChannelTests
{
    private static CreateOrganizationRequest BuildCreateOrganizationRequest(
        string orgSlug = "demo-club",
        string branchSlug = "demo-branch")
    {
        return new CreateOrganizationRequest(
            OrganizationSlug: orgSlug,
            OrganizationName: "Demo Club",
            BranchSlug: branchSlug,
            BranchName: "Demo Branch",
            BranchCity: "Dushanbe",
            PlanCode: OrganizationPlanCodeNames.Starter,
            SubscriptionStatus: SubscriptionStatusNames.Trial,
            Limits: new OrganizationLimitsDto(3, 60, 80, 20),
            OwnerUserName: "owner@demo-club.test",
            OwnerDisplayName: "Demo Owner",
            OrganizationOwnerInviteLifetime: TimeSpan.FromDays(7));
    }

    private static async Task<Guid> CreateOrganizationAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
        Assert.NotNull(body);
        return body.Organization.OrganizationId;
    }

    [Fact]
    public async Task PatchUpdateChannel_SavesAndReturnsBetaChannel()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateOrganizationAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/update-channel",
            new UpdateOrganizationUpdateChannelRequest("beta", null));
        var body = await response.Content.ReadFromJsonAsync<OrganizationDetailDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("beta", body.UpdateChannel);
        Assert.Null(body.PinnedClientVersion);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = await dbContext.Organizations.SingleAsync(org => org.OrganizationId == organizationId);
        Assert.Equal("beta", organization.UpdateChannel);

        var audit = await dbContext.AuditRecords
            .SingleAsync(record => record.Action == "tenancy.organization.update_channel.update" && record.Outcome == "Succeeded");
        Assert.Equal(organizationId, audit.OrganizationId);
        Assert.Contains("\"UpdateChannel\":\"beta\"", audit.DetailsJson);
    }

    [Fact]
    public async Task PatchUpdateChannel_PinsClientVersion_PersistsAndReturns()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateOrganizationAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/update-channel",
            new UpdateOrganizationUpdateChannelRequest("stable", "4.2.1"));
        var body = await response.Content.ReadFromJsonAsync<OrganizationDetailDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("4.2.1", body.PinnedClientVersion);

        var unpin = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/update-channel",
            new UpdateOrganizationUpdateChannelRequest("stable", null));
        var unpinBody = await unpin.Content.ReadFromJsonAsync<OrganizationDetailDto>();

        Assert.Equal(HttpStatusCode.OK, unpin.StatusCode);
        Assert.NotNull(unpinBody);
        Assert.Null(unpinBody.PinnedClientVersion);
    }

    [Fact]
    public async Task PatchUpdateChannel_WithUnknownChannel_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateOrganizationAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/update-channel",
            new UpdateOrganizationUpdateChannelRequest("nightly", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchUpdateChannel_WithUnknownOrganization_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{Guid.NewGuid():D}/update-channel",
            new UpdateOrganizationUpdateChannelRequest("beta", null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchUpdateChannel_WithoutAuth_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{Guid.NewGuid():D}/update-channel",
            new UpdateOrganizationUpdateChannelRequest("beta", null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
