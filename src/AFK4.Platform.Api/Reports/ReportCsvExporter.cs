using System.Globalization;
using System.Text;
using AFK4.Shared.Contracts.Reports;

namespace AFK4.Platform.Api.Reports;

public static class ReportCsvExporter
{
    private const string NewLine = "\r\n";

    public static string ExportShiftReport(ShiftReportResultDto report)
    {
        var builder = new StringBuilder();
        AppendLine(
            builder,
            "shift_id",
            "organization_id",
            "branch_id",
            "opened_by_staff_user_id",
            "closed_by_staff_user_id",
            "state",
            "starting_cash_currency",
            "starting_cash_minor_units",
            "cash_movements_minor_units",
            "pos_cash_payments_minor_units",
            "pos_refunds_minor_units",
            "billing_cash_impact_minor_units",
            "expected_cash_minor_units",
            "counted_cash_minor_units",
            "difference_minor_units",
            "opened_at_utc",
            "closed_at_utc");

        foreach (var row in report.Rows)
        {
            AppendLine(
                builder,
                row.ShiftId,
                row.OrganizationId,
                row.BranchId,
                row.OpenedByStaffUserId,
                row.ClosedByStaffUserId,
                row.State,
                row.StartingCash.CurrencyCode,
                row.StartingCash.MinorUnits,
                row.CashMovementsTotal.MinorUnits,
                row.PosCashPaymentsTotal.MinorUnits,
                row.PosRefundsTotal.MinorUnits,
                row.BillingCashImpactTotal.MinorUnits,
                row.ExpectedCash.MinorUnits,
                row.CountedCash?.MinorUnits,
                row.Difference?.MinorUnits,
                row.OpenedAtUtc,
                row.ClosedAtUtc);
        }

        return builder.ToString();
    }

    public static string ExportSalesReport(SalesReportResultDto report)
    {
        var builder = new StringBuilder();
        AppendLine(
            builder,
            "pos_sale_id",
            "organization_id",
            "branch_id",
            "shift_id",
            "created_by_staff_user_id",
            "state",
            "currency",
            "total_minor_units",
            "paid_minor_units",
            "refund_minor_units",
            "line_count",
            "item_quantity",
            "created_at_utc",
            "paid_at_utc",
            "refunded_at_utc",
            "voided_at_utc",
            "gross_cogs_minor_units",
            "refunded_cogs_minor_units",
            "net_cogs_minor_units");

        foreach (var row in report.Rows)
        {
            AppendLine(
                builder,
                row.PosSaleId,
                row.OrganizationId,
                row.BranchId,
                row.ShiftId,
                row.CreatedByStaffUserId,
                row.State,
                row.Total.CurrencyCode,
                row.Total.MinorUnits,
                row.PaidAmount.MinorUnits,
                row.RefundAmount.MinorUnits,
                row.LineCount,
                row.ItemQuantity,
                row.CreatedAtUtc,
                row.PaidAtUtc,
                row.RefundedAtUtc,
                row.VoidedAtUtc,
                row.GrossCostOfGoods.MinorUnits,
                row.RefundedCostOfGoods.MinorUnits,
                row.NetCostOfGoods.MinorUnits);
        }

        return builder.ToString();
    }

    public static string ExportGameplayTimeReport(GameplayTimeReportResultDto report)
    {
        var builder = new StringBuilder();
        AppendLine(
            builder,
            "session_id",
            "organization_id",
            "branch_id",
            "seat_id",
            "device_id",
            "created_by_staff_user_id",
            "player_kind",
            "player_account_id",
            "state",
            "duration_seconds",
            "package_seconds",
            "bonus_seconds",
            "revenue_currency",
            "revenue_minor_units",
            "started_at_utc",
            "ended_at_utc",
            "ends_at_utc");

        foreach (var row in report.Rows)
        {
            AppendLine(
                builder,
                row.SessionId,
                row.OrganizationId,
                row.BranchId,
                row.SeatId,
                row.DeviceId,
                row.CreatedByStaffUserId,
                row.PlayerKind,
                row.PlayerAccountId,
                row.State,
                row.DurationSeconds,
                row.PackageSeconds,
                row.BonusSeconds,
                row.GameplayRevenue.CurrencyCode,
                row.GameplayRevenue.MinorUnits,
                row.StartedAtUtc,
                row.EndedAtUtc,
                row.EndsAtUtc);
        }

        return builder.ToString();
    }

    public static string ExportCashOperationReport(CashOperationReportResultDto report)
    {
        var builder = new StringBuilder();
        AppendLine(
            builder,
            "operation_id",
            "organization_id",
            "branch_id",
            "shift_id",
            "created_by_staff_user_id",
            "source_type",
            "operation_type",
            "currency",
            "cash_impact_minor_units",
            "reason",
            "created_at_utc");

        foreach (var row in report.Rows)
        {
            AppendLine(
                builder,
                row.OperationId,
                row.OrganizationId,
                row.BranchId,
                row.ShiftId,
                row.CreatedByStaffUserId,
                row.SourceType,
                row.OperationType,
                row.CashImpact.CurrencyCode,
                row.CashImpact.MinorUnits,
                row.Reason,
                row.CreatedAtUtc);
        }

        return builder.ToString();
    }

    public static string ExportOperatorActionReport(OperatorActionReportResultDto report)
    {
        var builder = new StringBuilder();
        AppendLine(
            builder,
            "actor_staff_user_id",
            "actor_display_name",
            "action",
            "outcome",
            "count",
            "first_at_utc",
            "last_at_utc");

        foreach (var row in report.Rows)
        {
            AppendLine(
                builder,
                row.ActorStaffUserId,
                row.ActorDisplayName,
                row.Action,
                row.Outcome,
                row.Count,
                row.FirstAtUtc,
                row.LastAtUtc);
        }

        return builder.ToString();
    }

    private static void AppendLine(StringBuilder builder, params object?[] values)
    {
        for (var index = 0; index < values.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append(Escape(FormatValue(values[index])));
        }

        builder.Append(NewLine);
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTimeOffset dateTime => dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            Guid guid => guid.ToString("D"),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string Escape(string value)
    {
        if (!value.Contains(',') &&
            !value.Contains('"') &&
            !value.Contains('\r') &&
            !value.Contains('\n'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
