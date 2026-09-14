using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Players;

namespace AFK4.Platform.Api.Billing;

public interface IBillingCommandService
{
    Task<BillingCommandServiceResult<PlayerAccountDto>> CreatePlayerAccountAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreatePlayerAccountRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<PlayerAccountDto>> UpdatePlayerAccountAsync(
        Guid branchId,
        Guid actorStaffUserId,
        Guid playerAccountId,
        UpdatePlayerAccountRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<PlayerAccountDto>> SetPlayerActiveStateAsync(
        Guid branchId,
        Guid actorStaffUserId,
        Guid playerAccountId,
        SetPlayerActiveStateRequest request,
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

    /// <summary>
    /// Игрок гасит долг собственными деньгами с кошелька.
    ///
    /// Отличие от <see cref="PayDebtAsync"/> не в том, кто нажал, а в том, откуда деньги. У стойки
    /// они приходят наличными в ящик, поэтому там нужна открытая смена и хватает одной записи —
    /// уменьшения долга. Здесь деньги уже лежат на кошельке этого же игрока: записи две, долг
    /// минус и кошелёк минус, иначе сумма возникала бы из ниоткуда. Смена не нужна и не
    /// спрашивается: ящик не открывается, а игрок платит из дома в три часа ночи.
    /// </summary>
    Task<BillingCommandServiceResult<WalletSummaryDto>> PayDebtFromWalletAsync(
        Guid playerAccountId,
        Guid organizationId,
        Guid branchId,
        Guid actorStaffUserId,
        MoneyDto amount,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
