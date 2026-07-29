using System.Globalization;
using System.Text;
using AFK4.Shared.Contracts.Reports;

namespace AFK4.Platform.Api.Reports;

public static class OrganizationAdminReportCsvExporter
{
    public static string Export(OrganizationAdminShiftCashReportDto report)
    {
        var csv = new StringBuilder("section,id,state_or_type,currency,amount_minor_units,occurred_at_utc,detail\r\n");
        foreach (var shift in report.Shifts)
            Line(csv, "shift", shift.ShiftId, shift.State, shift.ExpectedCash.CurrencyCode, shift.Difference?.MinorUnits, shift.OpenedAtUtc, string.Empty);
        foreach (var operation in report.CashOperations)
            Line(csv, "cash_operation", operation.OperationId, operation.OperationType, operation.CashImpact.CurrencyCode, operation.CashImpact.MinorUnits, operation.CreatedAtUtc, operation.Reason);
        return csv.ToString();
    }

    public static string Export(OrganizationAdminRevenueReportDto report)
    {
        var csv = new StringBuilder("section,source,currency,amount_minor_units,from_date,to_date\r\n");
        Line(csv, "total", "gross", report.GrossRevenue.CurrencyCode, report.GrossRevenue.MinorUnits, report.Period.FromDate, report.Period.ToDate);
        Line(csv, "total", "refunds", report.Refunds.CurrencyCode, report.Refunds.MinorUnits, report.Period.FromDate, report.Period.ToDate);
        Line(csv, "total", "net", report.NetRevenue.CurrencyCode, report.NetRevenue.MinorUnits, report.Period.FromDate, report.Period.ToDate);
        foreach (var source in report.Sources)
            Line(csv, "source", source.Source, source.Revenue.CurrencyCode, source.Revenue.MinorUnits, report.Period.FromDate, report.Period.ToDate);
        return csv.ToString();
    }

    private static void Line(StringBuilder csv, string section, object id, string kind, string currency, long? amount, DateTimeOffset occurred, string detail) =>
        csv.Append(Csv(section)).Append(',').Append(Csv(id)).Append(',').Append(Csv(kind)).Append(',').Append(Csv(currency)).Append(',')
            .Append(amount?.ToString(CultureInfo.InvariantCulture)).Append(',').Append(occurred.ToString("O", CultureInfo.InvariantCulture)).Append(',').Append(Csv(detail)).Append("\r\n");

    private static void Line(StringBuilder csv, string section, string source, string currency, long amount, DateOnly from, DateOnly to) =>
        csv.Append(Csv(section)).Append(',').Append(Csv(source)).Append(',').Append(Csv(currency)).Append(',')
            .Append(amount.ToString(CultureInfo.InvariantCulture)).Append(',').Append(from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',').Append(to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append("\r\n");

    private static string Csv(object? value) => $"\"{value?.ToString()?.Replace("\"", "\"\"")}\"";
}
