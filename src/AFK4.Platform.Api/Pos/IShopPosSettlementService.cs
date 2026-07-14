using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Shop;

namespace AFK4.Platform.Api.Pos;

public sealed record ShopPosSaleRequest(
    Guid OrganizationId,
    Guid BranchId,
    Guid PlayerAccountId,
    Guid SessionId,
    Guid ActorStaffUserId,
    IReadOnlyList<ShopOrderLineInput> Lines,
    string ReferenceId,
    DateTimeOffset Now);

public sealed record ShopPosRefundRequest(
    Guid PosSaleId,
    Guid ShopOrderId,
    Guid WalletLedgerEntryId,
    Guid ActorStaffUserId,
    string Reason,
    DateTimeOffset Now);

public sealed record ShopPosSettlementResult(
    bool Succeeded,
    string? ErrorCode,
    PosSaleEntity? Sale,
    IReadOnlyList<PosSaleLineEntity> Lines,
    LedgerEntryEntity? WalletEntry,
    ReceiptEntity? Receipt)
{
    public static ShopPosSettlementResult Ok(
        PosSaleEntity sale,
        IReadOnlyList<PosSaleLineEntity> lines,
        LedgerEntryEntity walletEntry,
        ReceiptEntity receipt) =>
        new(true, null, sale, lines, walletEntry, receipt);

    public static ShopPosSettlementResult Reject(string code) =>
        new(false, code, null, [], null, null);
}

/// <summary>
/// Stages POS, wallet, receipt, and inventory changes for a caller-owned serializable
/// transaction. Implementations do not save or start, commit, or roll back transactions.
/// </summary>
public interface IShopPosSettlementService
{
    Task<ShopPosSettlementResult> CreatePaidWalletSaleAsync(
        ShopPosSaleRequest request,
        CancellationToken cancellationToken);

    Task<ShopPosSettlementResult> RefundPaidWalletSaleAsync(
        ShopPosRefundRequest request,
        CancellationToken cancellationToken);
}
