using AFK4.Shared.Contracts.Platform.Billing;

namespace AFK4.Platform.Api.Platform.Billing;

public interface ITenantSubscriptionService
{
    Task<BillingOperationResult<TenantSubscriptionDto>> GetAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<BillingOperationResult<TenantSubscriptionDto>> UpdateAsync(
        Guid organizationId,
        UpdateSubscriptionRequest request,
        CancellationToken cancellationToken);

    Task<BillingOperationResult<IReadOnlyList<SubscriptionListItemDto>>> ListAsync(
        string? status,
        string? planCode,
        CancellationToken cancellationToken);
}
