using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Features;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Entitlements;

public sealed class EfOrganizationEntitlements(PlatformDbContext dbContext) : IOrganizationEntitlements
{
    public async Task<bool> IsEnabledAsync(Guid organizationId, string featureKey, CancellationToken cancellationToken)
    {
        // Один round-trip вместо всей DescribeAsync: этот путь стоит на старте брони, заказа
        // в магазине и начисления кэшбэка на каждое списание за игровое время.
        var stages = await dbContext.Organizations
            .AsNoTracking()
            .Where(organization => organization.OrganizationId == organizationId)
            .Select(organization => new
            {
                DefaultValue = dbContext.PlatformFeatures
                    .Where(feature => feature.FeatureKey == featureKey)
                    .Select(feature => (bool?)feature.EnabledByDefault)
                    .FirstOrDefault(),
                PlanValue = dbContext.PlanFeatures
                    .Where(planFeature => planFeature.PlanCode == organization.PlanCode && planFeature.FeatureKey == featureKey)
                    .Select(planFeature => (bool?)planFeature.IsIncluded)
                    .FirstOrDefault(),
                OverrideValue = dbContext.OrganizationFeatureOverrides
                    .Where(featureOverride => featureOverride.OrganizationId == organizationId && featureOverride.FeatureKey == featureKey)
                    .Select(featureOverride => (bool?)featureOverride.IsEnabled)
                    .FirstOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (stages is null)
        {
            // Несуществующий клуб не получает ничего: здесь молчание значит «нет».
            return false;
        }

        var (isEnabled, _) = FeatureLadder.Resolve(stages.OverrideValue, stages.PlanValue, stages.DefaultValue);
        return isEnabled;
    }

    public async Task<IReadOnlyList<string>> ListEnabledAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var states = await DescribeAsync(organizationId, cancellationToken);
        return states.Where(state => state.IsEnabled).Select(state => state.FeatureKey).ToList();
    }

    public async Task<IReadOnlyList<OrganizationFeatureStateDto>> DescribeAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var planCode = await dbContext.Organizations
            .AsNoTracking()
            .Where(organization => organization.OrganizationId == organizationId)
            .Select(organization => organization.PlanCode)
            .SingleOrDefaultAsync(cancellationToken);
        if (planCode is null)
        {
            // Несуществующий клуб не получает ничего: здесь молчание значит «нет».
            return [];
        }

        var features = await dbContext.PlatformFeatures.AsNoTracking().ToListAsync(cancellationToken);
        var planOpinions = await dbContext.PlanFeatures
            .AsNoTracking()
            .Where(planFeature => planFeature.PlanCode == planCode)
            .ToDictionaryAsync(planFeature => planFeature.FeatureKey, planFeature => planFeature.IsIncluded, cancellationToken);
        var overrides = await dbContext.OrganizationFeatureOverrides
            .AsNoTracking()
            .Where(featureOverride => featureOverride.OrganizationId == organizationId)
            .ToDictionaryAsync(featureOverride => featureOverride.FeatureKey, cancellationToken);

        return features
            .OrderBy(feature => feature.FeatureKey, StringComparer.Ordinal)
            .Select(feature =>
            {
                overrides.TryGetValue(feature.FeatureKey, out var featureOverride);
                var planValue = planOpinions.TryGetValue(feature.FeatureKey, out var included)
                    ? included
                    : (bool?)null;

                var (isEnabled, level) = FeatureLadder.Resolve(featureOverride?.IsEnabled, planValue, feature.EnabledByDefault);

                return new OrganizationFeatureStateDto(
                    feature.FeatureKey,
                    feature.Name,
                    feature.Description,
                    isEnabled,
                    level,
                    featureOverride?.IsEnabled,
                    featureOverride?.Reason,
                    featureOverride?.SetAtUtc,
                    planValue,
                    feature.EnabledByDefault);
            })
            .ToList();
    }
}
