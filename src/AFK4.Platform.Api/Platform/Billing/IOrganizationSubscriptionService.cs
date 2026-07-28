using AFK4.Shared.Contracts.Platform.Billing;

namespace AFK4.Platform.Api.Platform.Billing;

public interface IOrganizationSubscriptionService
{
    Task<BillingOperationResult<OrganizationSubscriptionDto>> GetAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<BillingOperationResult<OrganizationSubscriptionDto>> UpdateAsync(
        Guid organizationId,
        UpdateSubscriptionRequest request,
        CancellationToken cancellationToken);

    Task<BillingOperationResult<IReadOnlyList<SubscriptionListItemDto>>> ListAsync(
        string? status,
        string? planCode,
        CancellationToken cancellationToken);
}
