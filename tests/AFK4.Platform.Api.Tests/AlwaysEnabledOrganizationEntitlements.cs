using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Shared.Contracts.Platform.Features;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Test double for billing/session/shop tests that construct <c>LoyaltyAccrualService</c> directly
/// and exercise its cashback math, not the feature-entitlements ladder (that ladder is covered
/// separately by FeatureGateTests and OrganizationEntitlementsTests) — so the feature is always "on".
/// </summary>
internal sealed class AlwaysEnabledOrganizationEntitlements : IOrganizationEntitlements
{
    public static readonly AlwaysEnabledOrganizationEntitlements Instance = new();

    public Task<bool> IsEnabledAsync(Guid organizationId, string featureKey, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task<IReadOnlyList<string>> ListEnabledAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(PlatformFeatureNames.All);

    public Task<IReadOnlyList<OrganizationFeatureStateDto>> DescribeAsync(
        Guid organizationId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
