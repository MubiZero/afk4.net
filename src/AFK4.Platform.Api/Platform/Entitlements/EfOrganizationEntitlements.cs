using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Features;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Entitlements;

public sealed class EfOrganizationEntitlements(PlatformDbContext dbContext) : IOrganizationEntitlements
{
    public async Task<bool> IsEnabledAsync(Guid organizationId, string featureKey, CancellationToken cancellationToken)
    {
        var states = await DescribeAsync(organizationId, cancellationToken);
        var state = states.SingleOrDefault(candidate => candidate.FeatureKey == featureKey);
        // Незнакомый ключ — выключено: молчание каталога не должно открывать доступ.
        return state?.IsEnabled ?? false;
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

                var (isEnabled, level) = featureOverride is not null
                    ? (featureOverride.IsEnabled, FeatureDecisionLevels.Override)
                    : planValue is { } fromPlan
                        ? (fromPlan, FeatureDecisionLevels.Plan)
                        : (feature.EnabledByDefault, FeatureDecisionLevels.Default);

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
