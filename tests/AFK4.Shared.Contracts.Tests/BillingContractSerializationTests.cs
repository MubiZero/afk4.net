using System.Text.Json;
using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Tests;

public sealed class BillingContractSerializationTests
{
    [Fact]
    public void WalletSummary_RoundTripsDerivedBalancesAndRecentEntries()
    {
        var entry = new LedgerEntryDto(
            LedgerEntryId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            PlayerAccountId: Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
            SessionId: null,
            PlayerPackageId: null,
            EntryType: LedgerEntryTypeNames.TopUp,
            AccountType: LedgerAccountTypeNames.Wallet,
            Amount: new MoneyDto("TJS", 5000),
            QuantitySeconds: 0,
            Description: "Cash top-up",
            Reason: "front-desk top-up",
            ReversesLedgerEntryId: null,
            CreatedByStaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"));
        var summary = new WalletSummaryDto(
            PlayerAccountId: entry.PlayerAccountId,
            WalletBalance: new MoneyDto("TJS", 5000),
            DebtBalance: new MoneyDto("TJS", 0),
            RecentEntries: [entry]);

        var json = JsonSerializer.Serialize(summary);
        var copy = JsonSerializer.Deserialize<WalletSummaryDto>(json);

        Assert.NotNull(copy);
        Assert.Equal(5000, copy.WalletBalance.MinorUnits);
        Assert.Equal(0, copy.DebtBalance.MinorUnits);
        Assert.Single(copy.RecentEntries);
        Assert.Equal(LedgerEntryTypeNames.TopUp, copy.RecentEntries[0].EntryType);
    }

    [Fact]
    public void SessionRequests_CanCarryBillingModeAndPlayerAccount()
    {
        var playerAccountId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
        var tariffVersionId = Guid.Parse("cccccccc-cccc-4ccc-cccc-cccccccccccc");
        var start = new AFK4.Shared.Contracts.Sessions.StartGuestSessionRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
            DurationMinutes: 60,
            TariffRuleVersionId: tariffVersionId.ToString("D"),
            IdempotencyKey: "start-prepaid-001",
            PlayerAccountId: playerAccountId,
            BillingMode: BillingModeNames.PrepaidWallet,
            TariffVersionId: tariffVersionId,
            PlayerPackageId: null);

        Assert.Equal(playerAccountId, start.PlayerAccountId);
        Assert.Equal(BillingModeNames.PrepaidWallet, start.BillingMode);
        Assert.Equal(tariffVersionId, start.TariffVersionId);
    }
}
