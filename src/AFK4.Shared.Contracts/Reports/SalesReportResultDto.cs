using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Reports;

public sealed record SalesReportResultDto(
    IReadOnlyList<SalesReportRowDto> Rows,
    int Limit,
    MoneyDto GrossSalesTotal,
    MoneyDto RefundsTotal,
    MoneyDto NetSalesTotal);
