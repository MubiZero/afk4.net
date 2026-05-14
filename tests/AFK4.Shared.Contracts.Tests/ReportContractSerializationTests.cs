using System.Text.Json;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Reports;

namespace AFK4.Shared.Contracts.Tests;

public sealed class ReportContractSerializationTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ShiftReportResultDto_RoundTrips()
    {
        var row = new ShiftReportRowDto(
            ShiftId: Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
            OrganizationId: Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            OpenedByStaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
            ClosedByStaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
            State: "closed",
            StartingCash: new MoneyDto("TJS", 50000),
            CashMovementsTotal: new MoneyDto("TJS", 15000),
            PosCashPaymentsTotal: new MoneyDto("TJS", 2400),
            PosRefundsTotal: new MoneyDto("TJS", -1200),
            BillingCashImpactTotal: new MoneyDto("TJS", 12500),
            ExpectedCash: new MoneyDto("TJS", 78700),
            CountedCash: new MoneyDto("TJS", 78000),
            Difference: new MoneyDto("TJS", -700),
            OpenedAtUtc: DateTimeOffset.Parse("2026-05-14T09:00:00Z"),
            ClosedAtUtc: DateTimeOffset.Parse("2026-05-14T18:00:00Z"));
        var result = new ShiftReportResultDto([row], Limit: 50);

        var copy = JsonSerializer.Deserialize<ShiftReportResultDto>(
            JsonSerializer.Serialize(result, Options),
            Options);

        Assert.NotNull(copy);
        var readBack = Assert.Single(copy.Rows);
        Assert.Equal(row.ShiftId, readBack.ShiftId);
        Assert.Equal(78700, readBack.ExpectedCash.MinorUnits);
        Assert.Equal(50, copy.Limit);
    }

    [Fact]
    public void SalesReportResultDto_RoundTrips()
    {
        var row = new SalesReportRowDto(
            PosSaleId: Guid.Parse("cccccccc-0000-0000-0000-000000000001"),
            OrganizationId: Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            ShiftId: Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
            CreatedByStaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
            State: "paid",
            Total: new MoneyDto("TJS", 2400),
            PaidAmount: new MoneyDto("TJS", 2400),
            RefundAmount: new MoneyDto("TJS", 0),
            LineCount: 1,
            ItemQuantity: 2,
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-14T12:00:00Z"),
            PaidAtUtc: DateTimeOffset.Parse("2026-05-14T12:01:00Z"),
            RefundedAtUtc: null,
            VoidedAtUtc: null);
        var result = new SalesReportResultDto(
            [row],
            Limit: 50,
            GrossSalesTotal: new MoneyDto("TJS", 2400),
            RefundsTotal: new MoneyDto("TJS", 0),
            NetSalesTotal: new MoneyDto("TJS", 2400));

        var copy = JsonSerializer.Deserialize<SalesReportResultDto>(
            JsonSerializer.Serialize(result, Options),
            Options);

        Assert.NotNull(copy);
        Assert.Equal(2400, copy.NetSalesTotal.MinorUnits);
        Assert.Equal(2, Assert.Single(copy.Rows).ItemQuantity);
    }
}
