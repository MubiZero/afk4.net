using AFK4.Platform.Api.Platform.Analytics;
using AFK4.Shared.Contracts.Billing;

namespace AFK4.Platform.Api.Tests.Platform.Analytics;

public sealed class BranchRevenueTests
{
    [Fact]
    public void PosNet_CountsPaymentsAndRefunds_IgnoringOtherKinds()
    {
        var posNet = BranchRevenue.PosNet(
        [
            ("payment", 5_000L),
            ("refund", -1_500L),
            ("deposit", 90_000L)
        ]);

        Assert.Equal(3_500L, posNet);
    }

    [Fact]
    public void PosNet_MatchesKindsCaseInsensitively()
    {
        Assert.Equal(700L, BranchRevenue.PosNet([("Payment", 700L)]));
    }

    [Fact]
    public void Gameplay_AddsChargesAndDebt_SubtractsRefunds()
    {
        var gameplay = BranchRevenue.Gameplay(
        [
            (LedgerEntryTypeNames.GameplayCharge, -4_000L),
            (LedgerEntryTypeNames.PostpaidDebt, 2_000L),
            (LedgerEntryTypeNames.Refund, 500L),
            (LedgerEntryTypeNames.TopUp, 100_000L)
        ]);

        Assert.Equal(4_000L + 2_000L - 500L, gameplay);
    }

    [Fact]
    public void Gameplay_IgnoresNegativePostpaidDebt()
    {
        // Погашение долга приходит той же строкой с отрицательной суммой — это не выручка суток,
        // выручка была зачтена в момент возникновения долга.
        Assert.Equal(0L, BranchRevenue.Gameplay([(LedgerEntryTypeNames.PostpaidDebt, -2_000L)]));
    }
}
