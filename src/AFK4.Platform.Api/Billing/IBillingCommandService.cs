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

    // Credits a wallet from a confirmed online payment (e.g. the dcgate webhook).
    // Unlike the counter top-up, this does NOT require an open cash shift: online money
    // never enters a cashier's drawer, so the ledger entry is recorded with no shift and
    // no human actor. Idempotent on request.IdempotencyKey (the payment intent id).
    Task<BillingCommandServiceResult<WalletSummaryDto>> CreditOnlineTopUpAsync(
        Guid playerAccountId,
        Guid branchId,
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
