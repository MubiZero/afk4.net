using AFK4.Shared.Contracts.Billing;

namespace AFK4.Platform.Api.Billing;

public interface IBillingCommandService
{
    Task<BillingCommandServiceResult<PlayerAccountDto>> CreatePlayerAccountAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreatePlayerAccountRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<WalletSummaryDto>> TopUpWalletAsync(
        Guid playerAccountId,
        Guid branchId,
        Guid actorStaffUserId,
        TopUpWalletRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<LedgerEntryDto>> RefundLedgerEntryAsync(
        Guid branchId,
        Guid actorStaffUserId,
        RefundLedgerEntryRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<WalletSummaryDto>> ManualCorrectionAsync(
        Guid playerAccountId,
        Guid branchId,
        Guid actorStaffUserId,
        ManualLedgerCorrectionRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<WalletSummaryDto>> PayDebtAsync(
        Guid playerAccountId,
        Guid branchId,
        Guid actorStaffUserId,
        PayDebtRequest request,
        CancellationToken cancellationToken);
}
