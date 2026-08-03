using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Platform.Organizations;
using AFK4.Shared.Contracts.Platform.Search;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformSearchEndpointTests
{
    [Fact]
    public async Task Search_ReturnsOrganizationClubAndOwnerWithCanonicalLinks()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var created = await CreateOrganizationAsync(client);
        await SeedOwnerAsync(factory, created);

        var response = await client.GetAsync("/api/platform/search?query=orion");
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, raw);
        var results = System.Text.Json.JsonSerializer.Deserialize<List<PlatformSearchResultDto>>(raw, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(results);
        Assert.Contains(results, item => item.Kind == "organization" && item.Href == $"/admin/organizations/{created.Organization.OrganizationId}");
        Assert.Contains(results, item => item.Kind == "club" && item.Href.EndsWith("?tab=clubs"));
        Assert.Contains(results, item => item.Kind == "owner" && item.Href.EndsWith("?tab=access"));
    }

    [Fact]
    public async Task Search_ExactIdFindsResourceAndShortQueryReturnsEmpty()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var created = await CreateOrganizationAsync(client);

        var exact = await client.GetFromJsonAsync<List<PlatformSearchResultDto>>($"/api/platform/search?query={created.Organization.OrganizationId}");
        var shortQuery = await client.GetFromJsonAsync<List<PlatformSearchResultDto>>("/api/platform/search?query=o");

        Assert.Contains(exact!, item => item.Id == created.Organization.OrganizationId);
        Assert.Empty(shortQuery!);
    }

    [Fact]
    public async Task Search_EnforcesPlatformAuthenticationAndPermission()
    {
        await using var factory = new PlatformApiFactory();
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/platform/search?query=orion")).StatusCode);

        using var denied = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, denied, userName: "denied@platform.test", roles: []);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync("/api/platform/search?query=orion")).StatusCode);
    }

    private static async Task<CreateOrganizationResponse> CreateOrganizationAsync(HttpClient client)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var response = await client.PostAsJsonAsync("/api/platform/organizations", new
        {
            organizationSlug = $"orion-{suffix}", organizationName = $"Orion Gaming {suffix}",
            branchSlug = "orion-center", branchName = "Orion Center", branchCity = "Tashkent",
            planCode = "starter", subscriptionStatus = "trial",
            ownerUserName = $"orion-owner-{suffix}@example.test", ownerDisplayName = "Orion Owner"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateOrganizationResponse>())!;
    }

    private static async Task SeedOwnerAsync(PlatformApiFactory factory, CreateOrganizationResponse created)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var staffId = Guid.NewGuid();
        var branchId = created.Organization.Branches.Single().BranchId;
        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = staffId, OrganizationId = created.Organization.OrganizationId,
            UserName = "orion.owner@example.test", NormalizedUserName = "ORION.OWNER@EXAMPLE.TEST",
            DisplayName = "Orion Owner", PasswordHash = "test", IsActive = true, CreatedAtUtc = DateTimeOffset.UtcNow
        });
        db.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
        {
            StaffRoleAssignmentId = Guid.NewGuid(), StaffUserId = staffId,
            OrganizationId = created.Organization.OrganizationId, BranchId = branchId,
            RoleName = OrganizationRoleNames.OrganizationOwner
        });
        await db.SaveChangesAsync();
    }
}
