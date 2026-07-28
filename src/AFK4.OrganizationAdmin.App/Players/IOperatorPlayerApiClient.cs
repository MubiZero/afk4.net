using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Operator;
using AFK4.Shared.Contracts.Packages;

namespace AFK4.OrganizationAdmin.App.Players;

public interface IOperatorPlayerApiClient
{
    Task<IReadOnlyList<PlayerSearchResultDto>> SearchPlayersAsync(
        Guid branchId,
        string query,
        int limit,
        CancellationToken cancellationToken);

    Task<PlayerAccountDto> CreatePlayerAsync(
        Guid branchId,
        CreatePlayerAccountRequest request,
        CancellationToken cancellationToken);

    Task<WalletSummaryDto> GetWalletSummaryAsync(
        Guid playerAccountId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PlayerPackageDto>> GetPlayerPackagesAsync(
        Guid playerAccountId,
        CancellationToken cancellationToken);

    Task<WalletSummaryDto> TopUpWalletAsync(
        Guid playerAccountId,
        TopUpWalletRequest request,
        CancellationToken cancellationToken);

    Task<WalletSummaryDto> PayDebtAsync(
        Guid playerAccountId,
        PayDebtRequest request,
        CancellationToken cancellationToken);
}

public sealed class UnconfiguredOperatorPlayerApiClient : IOperatorPlayerApiClient
{
    public Task<IReadOnlyList<PlayerSearchResultDto>> SearchPlayersAsync(
        Guid branchId,
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<PlayerAccountDto> CreatePlayerAsync(
        Guid branchId,
        CreatePlayerAccountRequest request,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<WalletSummaryDto> GetWalletSummaryAsync(
        Guid playerAccountId,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<IReadOnlyList<PlayerPackageDto>> GetPlayerPackagesAsync(
        Guid playerAccountId,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<WalletSummaryDto> TopUpWalletAsync(
        Guid playerAccountId,
        TopUpWalletRequest request,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<WalletSummaryDto> PayDebtAsync(
        Guid playerAccountId,
        PayDebtRequest request,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    private static InvalidOperationException CreateException()
    {
        return new InvalidOperationException("Operator player API client is not configured.");
    }
}
