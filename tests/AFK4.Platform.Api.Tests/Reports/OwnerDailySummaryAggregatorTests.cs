using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reports;
using AFK4.Shared.Contracts.Billing;

namespace AFK4.Platform.Api.Tests.Reports;

public sealed class OwnerDailySummaryAggregatorTests
{
    private static readonly Guid Alice = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid Bob = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly DateOnly Day = new(2026, 6, 1);

    [Fact]
    public void Build_GroupsByActorAndClassifiesRefundsCorrectionsWriteOffsCompsAndDiscrepancies()
    {
        var ledger = new[]
        {
            Entry(Alice, LedgerEntryTypeNames.Refund, LedgerAccountTypeNames.Wallet, -3000),
            Entry(Alice, LedgerEntryTypeNames.ManualCorrection, LedgerAccountTypeNames.Wallet, 1500),
            Entry(Bob, LedgerEntryTypeNames.ManualCorrection, LedgerAccountTypeNames.Debt, -5000),
        };
        var comps = new[]
        {
            CompAudit(Alice, 3000),
            CompAudit(Alice, 2000),
        };
        var shifts = new[]
        {
            ClosedShift(Bob, -2500),
            ClosedShift(Bob, 0), // balanced shift contributes nothing
        };
        var names = new Dictionary<Guid, string> { [Alice] = "Alice", [Bob] = "Bob" };

        var result = OwnerDailySummaryAggregator.Build(Day, "TJS", ledger, comps, shifts, names);

        Assert.Equal(Day, result.Date);
        Assert.Equal(3000, result.TotalRefundMinorUnits);
        Assert.Equal(2, result.TotalCompCount);
        Assert.Equal(5000, result.TotalCompValueMinorUnits);
        Assert.Equal(1500, result.TotalManualCorrectionMinorUnits);
        Assert.Equal(5000, result.TotalWriteOffMinorUnits);
        Assert.Equal(-2500, result.TotalDiscrepancyMinorUnits);

        var alice = Assert.Single(result.Rows, row => row.ActorStaffUserId == Alice);
        Assert.Equal("Alice", alice.ActorDisplayName);
        Assert.Equal(1, alice.RefundCount);
        Assert.Equal(3000, alice.RefundTotalMinorUnits);
        Assert.Equal(1, alice.ManualCorrectionCount);
        Assert.Equal(1500, alice.ManualCorrectionTotalMinorUnits);
        Assert.Equal(0, alice.WriteOffCount);
        Assert.Equal(2, alice.CompCount);
        Assert.Equal(5000, alice.CompValueMinorUnits);

        var bob = Assert.Single(result.Rows, row => row.ActorStaffUserId == Bob);
        Assert.Equal(1, bob.WriteOffCount);
        Assert.Equal(5000, bob.WriteOffTotalMinorUnits);
        Assert.Equal(0, bob.ManualCorrectionCount);
        Assert.Equal(1, bob.DiscrepancyShiftCount);
        Assert.Equal(-2500, bob.DiscrepancyTotalMinorUnits);
    }

    [Fact]
    public void Build_NoActivity_ReturnsEmptyRowsAndZeroTotals()
    {
        var result = OwnerDailySummaryAggregator.Build(
            Day, "TJS", [], [], [], new Dictionary<Guid, string>());

        Assert.Empty(result.Rows);
        Assert.Equal(0, result.TotalRefundMinorUnits);
        Assert.Equal(0, result.TotalCompCount);
    }

    [Fact]
    public void Build_PlayerShopSystemActor_UsesDeterministicDisplayNameWithoutStaffRow()
    {
        var result = OwnerDailySummaryAggregator.Build(
            Day,
            "TJS",
            [Entry(SystemActorIds.PlayerShop, LedgerEntryTypeNames.Reversal, LedgerAccountTypeNames.Wallet, 1200)],
            [],
            [],
            new Dictionary<Guid, string>());

        var row = Assert.Single(result.Rows);
        Assert.Equal(SystemActorIds.PlayerShop, row.ActorStaffUserId);
        Assert.Equal(SystemActorIds.PlayerShopDisplayName, row.ActorDisplayName);
    }

    private static LedgerEntryEntity Entry(Guid actor, string entryType, string accountType, long amount) =>
        new()
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PlayerAccountId = Guid.NewGuid(),
            EntryType = entryType,
            AccountType = accountType,
            AmountMinorUnits = amount,
            CurrencyCode = "TJS",
            CreatedByStaffUserId = actor,
            CreatedAtUtc = DateTimeOffset.Parse("2026-06-01T10:00:00Z")
        };

    private static AuditRecordEntity CompAudit(Guid actor, long amount) =>
        new()
        {
            AuditRecordId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ActorStaffUserId = actor,
            Action = AuditActionNames.SessionComp,
            TargetType = "Session",
            Outcome = AuditOutcome.Succeeded,
            SourceApp = "test",
            AmountMinorUnits = amount,
            CreatedAtUtc = DateTimeOffset.Parse("2026-06-01T11:00:00Z")
        };

    private static ShiftEntity ClosedShift(Guid closedBy, long difference) =>
        new()
        {
            ShiftId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            OpenedByStaffUserId = closedBy,
            ClosedByStaffUserId = closedBy,
            State = "closed",
            CurrencyCode = "TJS",
            DifferenceMinorUnits = difference,
            OpenedAtUtc = DateTimeOffset.Parse("2026-06-01T08:00:00Z"),
            ClosedAtUtc = DateTimeOffset.Parse("2026-06-01T16:00:00Z")
        };
}
