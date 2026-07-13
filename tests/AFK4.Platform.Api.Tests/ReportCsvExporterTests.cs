using AFK4.Platform.Api.Reports;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Reports;

namespace AFK4.Platform.Api.Tests;

public sealed class ReportCsvExporterTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid ShiftId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid StaffUserId = Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134");

    [Fact]
    public void ExportSalesReport_WritesCostOfGoodsColumns()
    {
        var report = new SalesReportResultDto(
            [
                new SalesReportRowDto(
                    Guid.Parse("77777777-7777-4777-8777-777777777777"),
                    OrganizationId,
                    BranchId,
                    ShiftId,
                    StaffUserId,
                    "refunded",
                    new MoneyDto("TJS", 2400),
                    new MoneyDto("TJS", 2400),
                    new MoneyDto("TJS", -2400),
                    1,
                    2,
                    DateTimeOffset.Parse("2026-05-14T12:00:00Z"),
                    DateTimeOffset.Parse("2026-05-14T12:01:00Z"),
                    DateTimeOffset.Parse("2026-05-14T12:02:00Z"),
                    null,
                    new MoneyDto("TJS", 800),
                    new MoneyDto("TJS", -800),
                    new MoneyDto("TJS", 0))
            ],
            50,
            new MoneyDto("TJS", 2400),
            new MoneyDto("TJS", -2400),
            new MoneyDto("TJS", 0),
            new MoneyDto("TJS", 800),
            new MoneyDto("TJS", -800),
            new MoneyDto("TJS", 0));

        var csv = ReportCsvExporter.ExportSalesReport(report);

        Assert.StartsWith(
            "pos_sale_id,organization_id,branch_id,shift_id,created_by_staff_user_id,state,currency,total_minor_units,paid_minor_units,refund_minor_units,gross_cogs_minor_units,refunded_cogs_minor_units,net_cogs_minor_units,line_count,item_quantity,created_at_utc,paid_at_utc,refunded_at_utc,voided_at_utc\r\n",
            csv);
        Assert.Contains(",2400,2400,-2400,800,-800,0,1,2,", csv);
    }

    [Fact]
    public void ExportShiftReport_WritesHeaderAndRows()
    {
        var report = new ShiftReportResultDto(
            [
                new ShiftReportRowDto(
                    ShiftId,
                    OrganizationId,
                    BranchId,
                    StaffUserId,
                    null,
                    "open",
                    new MoneyDto("TJS", 50000),
                    new MoneyDto("TJS", 1500),
                    new MoneyDto("TJS", 2400),
                    new MoneyDto("TJS", -500),
                    new MoneyDto("TJS", 10000),
                    new MoneyDto("TJS", 63400),
                    null,
                    null,
                    DateTimeOffset.Parse("2026-05-14T09:00:00Z"),
                    null)
            ],
            Limit: 50);

        var csv = ReportCsvExporter.ExportShiftReport(report);

        Assert.StartsWith(
            "shift_id,organization_id,branch_id,opened_by_staff_user_id,closed_by_staff_user_id,state,starting_cash_currency,starting_cash_minor_units,cash_movements_minor_units,pos_cash_payments_minor_units,pos_refunds_minor_units,billing_cash_impact_minor_units,expected_cash_minor_units,counted_cash_minor_units,difference_minor_units,opened_at_utc,closed_at_utc\r\n",
            csv);
        Assert.Contains($"{ShiftId:D},{OrganizationId:D},{BranchId:D},{StaffUserId:D},,open,TJS,50000,1500,2400,-500,10000,63400,,,2026-05-14T09:00:00.0000000+00:00,", csv);
    }

    [Fact]
    public void ExportOperatorActionReport_EscapesCsvValues()
    {
        var report = new OperatorActionReportResultDto(
            [
                new OperatorActionReportRowDto(
                    StaffUserId,
                    "Manager, \"One\"",
                    "reports.operator_actions.view",
                    "Succeeded",
                    Count: 2,
                    FirstAtUtc: DateTimeOffset.Parse("2026-05-14T10:00:00Z"),
                    LastAtUtc: DateTimeOffset.Parse("2026-05-14T11:00:00Z"))
            ],
            Limit: 50,
            TotalActionCount: 2);

        var csv = ReportCsvExporter.ExportOperatorActionReport(report);

        Assert.Contains("\"Manager, \"\"One\"\"\"", csv);
        Assert.Contains("reports.operator_actions.view,Succeeded,2,2026-05-14T10:00:00.0000000+00:00,2026-05-14T11:00:00.0000000+00:00", csv);
    }
}
