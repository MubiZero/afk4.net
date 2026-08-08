using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformBranchEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 10, 0, 0, TimeSpan.Zero);

    private static async Task<Guid> SeedOrganizationAsync(
        PlatformApiFactory factory,
        int? maxBranches,
        int existingBranchCount = 1,
        string planCode = "growth")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var organizationId = Guid.NewGuid();
        dbContext.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = organizationId,
            Slug = "club-" + organizationId.ToString("N")[..8],
            Name = "Клуб",
            Status = OrganizationStatusNames.Active,
            PlanCode = planCode,
            SubscriptionStatus = SubscriptionStatusNames.Trial,
            LimitsJson = OrganizationLimitsJson.Serialize(new OrganizationLimitsDto(maxBranches, null, null, null)),
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });

        for (var i = 0; i < existingBranchCount; i++)
        {
            var branchId = Guid.NewGuid();
            dbContext.Branches.Add(new BranchEntity
            {
                BranchId = branchId,
                OrganizationId = organizationId,
                Slug = "existing-branch-" + i,
                Name = "Существующий филиал " + i,
                City = "Dushanbe",
                CreatedAtUtc = Now
            });
        }

        await dbContext.SaveChangesAsync();
        return organizationId;
    }

    private static CreateBranchRequest BuildCreateBranchRequest(
        string slug = "new-branch",
        string name = "Новый филиал",
        string city = "Khujand",
        string? preferredTimeZone = null) =>
        new(Slug: slug, Name: name, City: city, PreferredTimeZone: preferredTimeZone);

    [Fact]
    public async Task Post_RequiresAuthentication()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var organizationId = await SeedOrganizationAsync(factory, maxBranches: null);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{organizationId}/branches",
            BuildCreateBranchRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_RequiresCreateOrganizationPermission()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(
            factory, client, roles: [PlatformAdminRoleNames.PlatformSupport]);
        var organizationId = await SeedOrganizationAsync(factory, maxBranches: null);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{organizationId}/branches",
            BuildCreateBranchRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await dbContext.Branches.CountAsync(branch => branch.OrganizationId == organizationId));
        var audit = await dbContext.AuditRecords.SingleAsync(record => record.Action == "tenancy.branch.create");
        Assert.Equal("Denied", audit.Outcome);
        Assert.Equal(admin.PlatformAdminId, audit.ActorPlatformAdminUserId);
    }

    [Fact]
    public async Task Post_CreatesBranchWithDefaultZone()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await SeedOrganizationAsync(factory, maxBranches: 3, existingBranchCount: 1);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{organizationId}/branches",
            BuildCreateBranchRequest());
        var body = await response.Content.ReadFromJsonAsync<OrganizationBranchDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("new-branch", body!.Slug);
        Assert.Equal("Новый филиал", body.Name);
        Assert.Equal("Khujand", body.City);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(2, await dbContext.Branches.CountAsync(branch => branch.OrganizationId == organizationId));

        var newBranch = await dbContext.Branches.SingleAsync(branch => branch.BranchId == body.BranchId);
        Assert.Equal("Asia/Dushanbe", newBranch.PreferredTimeZone);

        var zone = await dbContext.Zones.SingleAsync(zone => zone.BranchId == newBranch.BranchId);
        Assert.Equal("Общий зал", zone.Name);

        var audit = await dbContext.AuditRecords.SingleAsync(record => record.Action == "tenancy.branch.create");
        Assert.Equal("Succeeded", audit.Outcome);
        Assert.Equal(organizationId, audit.OrganizationId);
    }

    [Fact]
    public async Task Post_RefusesWithNumbers_WhenBranchLimitReached()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await SeedOrganizationAsync(factory, maxBranches: 1, existingBranchCount: 1);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{organizationId}/branches",
            BuildCreateBranchRequest());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(PlanLimitNames.ReachedCode, body.GetProperty("code").GetString());
        Assert.Equal(1, body.GetProperty("planLimit").GetProperty("limit").GetInt32());
        Assert.Equal(1, body.GetProperty("planLimit").GetProperty("current").GetInt32());

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await dbContext.Branches.CountAsync(branch => branch.OrganizationId == organizationId));
    }

    [Fact]
    public async Task Post_RejectsDuplicateSlugWithinOrganization()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await SeedOrganizationAsync(factory, maxBranches: 1, existingBranchCount: 1);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{organizationId}/branches",
            BuildCreateBranchRequest(slug: "existing-branch-0"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.TryGetProperty("code", out _));

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await dbContext.Branches.CountAsync(branch => branch.OrganizationId == organizationId));
    }

    [Fact]
    public async Task Post_ReturnsNotFound_ForUnknownOrganization()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{Guid.NewGuid()}/branches",
            BuildCreateBranchRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
