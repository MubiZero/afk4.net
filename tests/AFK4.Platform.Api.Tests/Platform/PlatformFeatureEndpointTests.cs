using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Features;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformFeatureEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 10, 0, 0, TimeSpan.Zero);

    private static async Task<Guid> SeedOrganizationAsync(PlatformApiFactory factory, string planCode = "growth")
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
            LimitsJson = OrganizationLimitsJson.Serialize(new OrganizationLimitsDto(null, null, null, null)),
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });

        await dbContext.SaveChangesAsync();
        return organizationId;
    }

    [Fact]
    public async Task Get_RequiresAuthentication()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var organizationId = await SeedOrganizationAsync(factory);

        var response = await client.GetAsync($"/api/platform/organizations/{organizationId}/features");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithoutViewOrganizationsPermission_WritesDeniedAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(
            factory, client, roles: ["someone_without_platform_permissions"]);
        var organizationId = await SeedOrganizationAsync(factory);

        var response = await client.GetAsync($"/api/platform/organizations/{organizationId}/features");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync(
            record => record.Action == "platform.organizations.features.view");
        Assert.Equal("Denied", audit.Outcome);
        Assert.Equal(organizationId, audit.OrganizationId);
        Assert.Equal(admin.PlatformAdminId, audit.ActorPlatformAdminUserId);
    }

    [Fact]
    public async Task Get_ReturnsEveryFeatureWithDecisionLevel()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await SeedOrganizationAsync(factory);

        var response = await client.GetAsync($"/api/platform/organizations/{organizationId}/features");
        var body = await response.Content.ReadFromJsonAsync<List<OrganizationFeatureStateDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(PlatformFeatureNames.All.Count, body!.Count);
        Assert.All(body, state => Assert.Equal(FeatureDecisionLevels.Default, state.DecisionLevel));
    }

    [Fact]
    public async Task Put_RequiresManageFeaturesPermission()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(
            factory, client, roles: [PlatformAdminRoleNames.PlatformSupport]);
        var organizationId = await SeedOrganizationAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/platform/organizations/{organizationId}/features/{PlatformFeatureNames.Loyalty}",
            new SetFeatureOverrideRequest(false, "Клуб просил выключить"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(0, await dbContext.OrganizationFeatureOverrides.CountAsync());
        var audit = await dbContext.AuditRecords.SingleAsync(
            record => record.Action == "platform.organizations.features.override.set");
        Assert.Equal("Denied", audit.Outcome);
        Assert.Equal(admin.PlatformAdminId, audit.ActorPlatformAdminUserId);
    }

    [Fact]
    public async Task Put_SetsOverrideAndWritesAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await SeedOrganizationAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/platform/organizations/{organizationId}/features/{PlatformFeatureNames.Loyalty}",
            new SetFeatureOverrideRequest(false, "Клуб просил выключить"));
        var body = await response.Content.ReadFromJsonAsync<List<OrganizationFeatureStateDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        var loyalty = body!.Single(state => state.FeatureKey == PlatformFeatureNames.Loyalty);
        Assert.Equal(FeatureDecisionLevels.Override, loyalty.DecisionLevel);
        Assert.False(loyalty.IsEnabled);
        Assert.Equal("Клуб просил выключить", loyalty.OverrideReason);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var stored = await dbContext.OrganizationFeatureOverrides.SingleAsync(
            featureOverride => featureOverride.OrganizationId == organizationId
                && featureOverride.FeatureKey == PlatformFeatureNames.Loyalty);
        Assert.False(stored.IsEnabled);
        Assert.Equal("Клуб просил выключить", stored.Reason);
        Assert.Equal(admin.PlatformAdminId, stored.SetByPlatformAdminUserId);

        var audit = await dbContext.AuditRecords.SingleAsync(
            record => record.Action == "platform.organizations.features.override.set");
        Assert.Equal("Succeeded", audit.Outcome);
        Assert.Equal(organizationId, audit.OrganizationId);
        Assert.Equal(admin.PlatformAdminId, audit.ActorPlatformAdminUserId);
        Assert.Equal(PlatformFeatureNames.Loyalty, audit.TargetId);
    }

    [Fact]
    public async Task Put_RejectsEmptyReason()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await SeedOrganizationAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/platform/organizations/{organizationId}/features/{PlatformFeatureNames.Loyalty}",
            new SetFeatureOverrideRequest(false, "   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(0, await dbContext.OrganizationFeatureOverrides.CountAsync());
    }

    [Fact]
    public async Task Put_RejectsUnknownFeatureKey()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await SeedOrganizationAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/platform/organizations/{organizationId}/features/not-a-real-feature",
            new SetFeatureOverrideRequest(false, "Клуб просил выключить"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(0, await dbContext.OrganizationFeatureOverrides.CountAsync());
    }

    [Fact]
    public async Task Put_ReplacesAnExistingOverride()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await SeedOrganizationAsync(factory);

        await client.PutAsJsonAsync(
            $"/api/platform/organizations/{organizationId}/features/{PlatformFeatureNames.Loyalty}",
            new SetFeatureOverrideRequest(false, "Первая причина"));
        var response = await client.PutAsJsonAsync(
            $"/api/platform/organizations/{organizationId}/features/{PlatformFeatureNames.Loyalty}",
            new SetFeatureOverrideRequest(true, "Вторая причина"));
        var body = await response.Content.ReadFromJsonAsync<List<OrganizationFeatureStateDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var loyalty = body!.Single(state => state.FeatureKey == PlatformFeatureNames.Loyalty);
        Assert.True(loyalty.IsEnabled);
        Assert.Equal("Вторая причина", loyalty.OverrideReason);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var stored = await dbContext.OrganizationFeatureOverrides.SingleAsync(
            featureOverride => featureOverride.OrganizationId == organizationId
                && featureOverride.FeatureKey == PlatformFeatureNames.Loyalty);
        Assert.True(stored.IsEnabled);
        Assert.Equal("Вторая причина", stored.Reason);
    }

    [Fact]
    public async Task Delete_RemovesOverrideAndFallsBackToPlan()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await SeedOrganizationAsync(factory);

        await client.PutAsJsonAsync(
            $"/api/platform/organizations/{organizationId}/features/{PlatformFeatureNames.Loyalty}",
            new SetFeatureOverrideRequest(false, "Клуб просил выключить"));

        var response = await client.DeleteAsync(
            $"/api/platform/organizations/{organizationId}/features/{PlatformFeatureNames.Loyalty}");
        var body = await response.Content.ReadFromJsonAsync<List<OrganizationFeatureStateDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var loyalty = body!.Single(state => state.FeatureKey == PlatformFeatureNames.Loyalty);
        Assert.Equal(FeatureDecisionLevels.Default, loyalty.DecisionLevel);
        Assert.True(loyalty.IsEnabled);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(0, await dbContext.OrganizationFeatureOverrides.CountAsync());

        var audit = await dbContext.AuditRecords.SingleAsync(
            record => record.Action == "platform.organizations.features.override.clear");
        Assert.Equal("Succeeded", audit.Outcome);
        Assert.Equal(organizationId, audit.OrganizationId);
        Assert.Equal(admin.PlatformAdminId, audit.ActorPlatformAdminUserId);
        Assert.Equal(PlatformFeatureNames.Loyalty, audit.TargetId);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_ForUnknownOrganization()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.GetAsync($"/api/platform/organizations/{Guid.NewGuid()}/features");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
