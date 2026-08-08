using AFK4.Shared.Contracts.Platform.Features;

namespace AFK4.Platform.Api.Platform.Entitlements;

/// <summary>
/// Единственное место, где считается, что клубу можно. Лестница: ручное исключение для клуба →
/// мнение тарифа → умолчание фичи. Второй экземпляр этого правила разошёлся бы с первым молча.
/// </summary>
public interface IOrganizationEntitlements
{
    Task<bool> IsEnabledAsync(Guid organizationId, string featureKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListEnabledAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrganizationFeatureStateDto>> DescribeAsync(Guid organizationId, CancellationToken cancellationToken);
}
