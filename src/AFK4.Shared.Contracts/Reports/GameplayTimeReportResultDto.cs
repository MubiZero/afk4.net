using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Reports;

public sealed record GameplayTimeReportResultDto(
    IReadOnlyList<GameplayTimeReportRowDto> Rows,
    int Limit,
    int TotalDurationSeconds,
    int TotalPackageSeconds,
    int TotalBonusSeconds,
    MoneyDto GameplayRevenueTotal);
