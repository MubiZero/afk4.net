using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Shared.Contracts.Platform.Features;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform.Entitlements;

public sealed class OrganizationEntitlementsTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 10, 0, 0, TimeSpan.Zero);

    private static async Task<Guid> SeedAsync(PlatformDbContext db, string planCode = "growth")
    {
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = organizationId,
            Slug = "club-" + organizationId.ToString("N")[..8],
            Name = "Клуб",
            Status = OrganizationStatusNames.Active,
            PlanCode = planCode,
            LimitsJson = OrganizationLimitsJson.Serialize(new OrganizationLimitsDto(null, null, null, null)),
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return organizationId;
    }

    [Fact]
    public async Task Default_WinsWhenNobodyElseSpoke()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organizationId = await SeedAsync(db);
        var entitlements = scope.ServiceProvider.GetRequiredService<IOrganizationEntitlements>();

        var states = await entitlements.DescribeAsync(organizationId, CancellationToken.None);

        Assert.Equal(PlatformFeatureNames.All.Count, states.Count);
        Assert.All(states, state =>
        {
            Assert.True(state.IsEnabled);
            Assert.Equal(FeatureDecisionLevels.Default, state.DecisionLevel);
        });
    }

    [Fact]
    public async Task Plan_BeatsDefault()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organizationId = await SeedAsync(db, "starter");
        db.PlanFeatures.Add(new PlanFeatureEntity
        {
            PlanFeatureId = Guid.NewGuid(),
            PlanCode = "starter",
            FeatureKey = PlatformFeatureNames.Loyalty,
            IsIncluded = false
        });
        await db.SaveChangesAsync();
        var entitlements = scope.ServiceProvider.GetRequiredService<IOrganizationEntitlements>();

        var isEnabled = await entitlements.IsEnabledAsync(organizationId, PlatformFeatureNames.Loyalty, CancellationToken.None);
        var states = await entitlements.DescribeAsync(organizationId, CancellationToken.None);
        var loyalty = states.Single(state => state.FeatureKey == PlatformFeatureNames.Loyalty);

        Assert.False(isEnabled);
        Assert.Equal(FeatureDecisionLevels.Plan, loyalty.DecisionLevel);
    }

    [Fact]
    public async Task Override_BeatsPlanAndDefault()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organizationId = await SeedAsync(db, "starter");
        db.PlanFeatures.Add(new PlanFeatureEntity
        {
            PlanFeatureId = Guid.NewGuid(),
            PlanCode = "starter",
            FeatureKey = PlatformFeatureNames.Loyalty,
            IsIncluded = true
        });
        db.OrganizationFeatureOverrides.Add(new OrganizationFeatureOverrideEntity
        {
            OrganizationFeatureOverrideId = Guid.NewGuid(),
            OrganizationId = organizationId,
            FeatureKey = PlatformFeatureNames.Loyalty,
            IsEnabled = false,
            Reason = "Клуб просил выключить",
            SetByPlatformAdminUserId = Guid.NewGuid(),
            SetAtUtc = Now
        });
        await db.SaveChangesAsync();
        var entitlements = scope.ServiceProvider.GetRequiredService<IOrganizationEntitlements>();

        var isEnabled = await entitlements.IsEnabledAsync(organizationId, PlatformFeatureNames.Loyalty, CancellationToken.None);
        var states = await entitlements.DescribeAsync(organizationId, CancellationToken.None);
        var loyalty = states.Single(state => state.FeatureKey == PlatformFeatureNames.Loyalty);

        Assert.False(isEnabled);
        Assert.Equal(FeatureDecisionLevels.Override, loyalty.DecisionLevel);
    }

    [Fact]
    public async Task Override_CanTurnOnWhatPlanTurnedOff()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organizationId = await SeedAsync(db, "starter");
        db.PlanFeatures.Add(new PlanFeatureEntity
        {
            PlanFeatureId = Guid.NewGuid(),
            PlanCode = "starter",
            FeatureKey = PlatformFeatureNames.Loyalty,
            IsIncluded = false
        });
        db.OrganizationFeatureOverrides.Add(new OrganizationFeatureOverrideEntity
        {
            OrganizationFeatureOverrideId = Guid.NewGuid(),
            OrganizationId = organizationId,
            FeatureKey = PlatformFeatureNames.Loyalty,
            IsEnabled = true,
            Reason = "Пилот для этого клуба",
            SetByPlatformAdminUserId = Guid.NewGuid(),
            SetAtUtc = Now
        });
        await db.SaveChangesAsync();
        var entitlements = scope.ServiceProvider.GetRequiredService<IOrganizationEntitlements>();

        var isEnabled = await entitlements.IsEnabledAsync(organizationId, PlatformFeatureNames.Loyalty, CancellationToken.None);
        var states = await entitlements.DescribeAsync(organizationId, CancellationToken.None);
        var loyalty = states.Single(state => state.FeatureKey == PlatformFeatureNames.Loyalty);

        Assert.True(isEnabled);
        Assert.Equal(FeatureDecisionLevels.Override, loyalty.DecisionLevel);
    }

    [Fact]
    public async Task Describe_ReportsWhichLevelDecided()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organizationId = await SeedAsync(db, "starter");
        db.PlanFeatures.Add(new PlanFeatureEntity
        {
            PlanFeatureId = Guid.NewGuid(),
            PlanCode = "starter",
            FeatureKey = PlatformFeatureNames.Loyalty,
            IsIncluded = false
        });
        var setAt = Now.AddHours(2);
        db.OrganizationFeatureOverrides.Add(new OrganizationFeatureOverrideEntity
        {
            OrganizationFeatureOverrideId = Guid.NewGuid(),
            OrganizationId = organizationId,
            FeatureKey = PlatformFeatureNames.Loyalty,
            IsEnabled = true,
            Reason = "Пилот для этого клуба",
            SetByPlatformAdminUserId = Guid.NewGuid(),
            SetAtUtc = setAt
        });
        await db.SaveChangesAsync();
        var entitlements = scope.ServiceProvider.GetRequiredService<IOrganizationEntitlements>();

        var states = await entitlements.DescribeAsync(organizationId, CancellationToken.None);
        var loyalty = states.Single(state => state.FeatureKey == PlatformFeatureNames.Loyalty);

        Assert.Equal(FeatureDecisionLevels.Override, loyalty.DecisionLevel);
        Assert.True(loyalty.IsEnabled);
        Assert.Equal(true, loyalty.OverrideValue);
        Assert.Equal("Пилот для этого клуба", loyalty.OverrideReason);
        Assert.Equal(setAt, loyalty.OverrideSetAtUtc);
        Assert.Equal(false, loyalty.PlanValue);
        Assert.True(loyalty.DefaultValue);
    }

    [Fact]
    public async Task ListEnabled_ReturnsOnlyEnabledKeys()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organizationId = await SeedAsync(db, "starter");
        db.PlanFeatures.Add(new PlanFeatureEntity
        {
            PlanFeatureId = Guid.NewGuid(),
            PlanCode = "starter",
            FeatureKey = PlatformFeatureNames.Loyalty,
            IsIncluded = false
        });
        await db.SaveChangesAsync();
        var entitlements = scope.ServiceProvider.GetRequiredService<IOrganizationEntitlements>();

        var enabled = await entitlements.ListEnabledAsync(organizationId, CancellationToken.None);

        Assert.DoesNotContain(PlatformFeatureNames.Loyalty, enabled);
        Assert.Equal(PlatformFeatureNames.All.Count - 1, enabled.Count);
        foreach (var featureKey in PlatformFeatureNames.All.Where(key => key != PlatformFeatureNames.Loyalty))
        {
            Assert.Contains(featureKey, enabled);
        }
    }

    [Fact]
    public async Task UnknownOrganization_ReportsEverythingDisabled()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var entitlements = scope.ServiceProvider.GetRequiredService<IOrganizationEntitlements>();

        var enabled = await entitlements.ListEnabledAsync(Guid.NewGuid(), CancellationToken.None);
        var isEnabled = await entitlements.IsEnabledAsync(Guid.NewGuid(), PlatformFeatureNames.Loyalty, CancellationToken.None);

        Assert.Empty(enabled);
        Assert.False(isEnabled);
    }
}
