using AFK4.Shared.Contracts.Operator;

namespace AFK4.Platform.Api.Billing;

public interface IOperatorReferenceDataService
{
    Task<IReadOnlyList<PlayerSearchResultDto>> SearchPlayersAsync(
        Guid organizationId,
        Guid branchId,
        string? query,
        int limit,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TariffOptionDto>> GetTariffOptionsAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PackageOptionDto>> GetPackageOptionsAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken);
}
