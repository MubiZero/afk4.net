using AFK4.Shared.Contracts.Tariffs;

namespace AFK4.Platform.Api.Billing;

public interface ITariffService
{
    Task<BillingCommandServiceResult<TariffDto>> CreateTariffAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreateTariffRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<TariffVersionDto>> CreateTariffVersionAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreateTariffVersionRequest request,
        CancellationToken cancellationToken);

    Task<TariffCalculationResult?> CalculateAsync(
        Guid branchId,
        CalculateTariffRequest request,
        CancellationToken cancellationToken);
}
