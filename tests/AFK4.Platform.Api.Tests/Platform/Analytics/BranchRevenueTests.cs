using AFK4.Platform.Api.Platform.Analytics;
using AFK4.Shared.Contracts.Billing;

namespace AFK4.Platform.Api.Tests.Platform.Analytics;

public sealed class BranchRevenueTests
{
    [Fact]
    public void Gameplay_AddsChargesAndDebt_SubtractsGameRefunds_IgnoresEverythingElse()
    {
        var gameplay = BranchRevenue.Gameplay(
        [
            (LedgerEntryTypeNames.GameplayCharge, -4_000L, true),
            (LedgerEntryTypeNames.PostpaidDebt, 2_000L, true),
            (LedgerEntryTypeNames.Refund, 500L, true),
            (LedgerEntryTypeNames.TopUp, 100_000L, false)
        ]);

        Assert.Equal(4_000L + 2_000L - 500L, gameplay);
    }

    // Возврат пополнения кошелька (запись без сессии) выручкой не был: вычитать его — занижать день.
    [Fact]
    public void Gameplay_DoesNotSubtractAWalletRefundWithoutASession()
    {
        Assert.Equal(0L, BranchRevenue.Gameplay([(LedgerEntryTypeNames.Refund, -1_000L, false)]));
    }

    [Fact]
    public void Gameplay_IgnoresNegativePostpaidDebt()
    {
        // Погашение долга приходит той же строкой с отрицательной суммой — это не выручка суток,
        // выручка была зачтена в момент возникновения долга.
        Assert.Equal(0L, BranchRevenue.Gameplay([(LedgerEntryTypeNames.PostpaidDebt, -2_000L, true)]));
    }

    [Fact]
    public void GoodsMovements_CountAPaidSaleOnItsDay_AndItsRefundOnTheRefundDay()
    {
        var paid = DateTimeOffset.Parse("2026-10-20T10:00:00Z");
        var refunded = DateTimeOffset.Parse("2026-10-21T09:00:00Z");

        Assert.Equal([(paid, 3_000L)], BranchRevenue.GoodsMovements(paid, null, 3_000));
        Assert.Equal([(paid, 3_000L), (refunded, -3_000L)], BranchRevenue.GoodsMovements(paid, refunded, 3_000));
        // Неоплаченный чек (черновик, аннулирован) не двигал денег.
        Assert.Empty(BranchRevenue.GoodsMovements(null, null, 3_000));
    }
}
