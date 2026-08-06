using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformOrganizationProfileEndpointTests
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
    public async Task PatchProfile_RenamesOrganizationAndPersistsAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateOrganizationAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}",
            new UpdateOrganizationProfileRequest(
                Name: "Renamed Club",
                ContactEmail: "billing@renamed-club.test",
                ContactPhone: "+992000000000",
                LegalDetails: "OOO Renamed Club, TIN 000000000"));
        var body = await response.Content.ReadFromJsonAsync<OrganizationDetailDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("Renamed Club", body.Name);
        Assert.Equal("billing@renamed-club.test", body.ContactEmail);
        Assert.Equal("+992000000000", body.ContactPhone);
        Assert.Equal("OOO Renamed Club, TIN 000000000", body.LegalDetails);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = await dbContext.Organizations.SingleAsync(org => org.OrganizationId == organizationId);
        Assert.Equal("Renamed Club", organization.Name);
        Assert.Equal("billing@renamed-club.test", organization.ContactEmail);

        var audit = await dbContext.AuditRecords
            .Where(record => record.Action == "tenancy.organization.profile.update" && record.Outcome == "Succeeded")
            .SingleAsync();
        Assert.Equal(organizationId, audit.OrganizationId);
        Assert.Contains("\"NewName\":\"Renamed Club\"", audit.DetailsJson);
    }

    [Fact]
    public async Task PatchProfile_WithBlankName_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateOrganizationAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}",
            new UpdateOrganizationProfileRequest(
                Name: "   ",
                ContactEmail: null,
                ContactPhone: null,
                LegalDetails: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchProfile_WithoutPermission_Returns403()
    {
        await using var factory = new PlatformApiFactory();
        using var adminClient = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, adminClient);

        var organizationId = await CreateOrganizationAsync(adminClient);

        using var supportClient = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(
            factory,
            supportClient,
            userName: "support@platform.test",
            roles: [PlatformAdminRoleNames.PlatformSupport]);

        var response = await supportClient.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}",
            new UpdateOrganizationProfileRequest(
                Name: "Renamed Club",
                ContactEmail: null,
                ContactPhone: null,
                LegalDetails: null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
