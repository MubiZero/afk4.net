using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Reports;

public sealed record CashOperationReportResultDto(
    IReadOnlyList<CashOperationReportRowDto> Rows,
    int Limit,
    MoneyDto CashInTotal,
    MoneyDto CashOutTotal,
    MoneyDto NetCashTotal);
