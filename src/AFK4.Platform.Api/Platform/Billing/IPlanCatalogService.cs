using AFK4.Shared.Contracts.Platform.Billing;

namespace AFK4.Platform.Api.Platform.Billing;

public interface IPlanCatalogService
{
    Task<IReadOnlyList<SubscriptionPlanDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<SubscriptionPlanDto?> GetAsync(string planCode, CancellationToken cancellationToken);

    Task<BillingOperationResult<SubscriptionPlanDto>> CreateAsync(
        CreatePlanRequest request,
        CancellationToken cancellationToken);

    Task<BillingOperationResult<SubscriptionPlanDto>> UpdateAsync(
        string planCode,
        UpdatePlanRequest request,
        CancellationToken cancellationToken);
}
