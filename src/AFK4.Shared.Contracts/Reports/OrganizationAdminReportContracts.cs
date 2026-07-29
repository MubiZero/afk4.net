using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Reports;

public sealed record OrganizationAdminReportPeriodDto(
    DateOnly FromDate,
    DateOnly ToDate,
    string TimeZone,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc);

public sealed record OrganizationAdminReportAttentionDto(
    string Kind,
    string Title,
    string Detail,
    Guid? TargetId,
    MoneyDto? Amount);

public sealed record OrganizationAdminReportFiguresDto(
    MoneyDto NetRevenue,
    MoneyDto GameplayRevenue,
    MoneyDto PosNetSales,
    int GameplaySeconds);

public sealed record OrganizationAdminRevenueTrendPointDto(DateOnly Date, MoneyDto NetRevenue);

public sealed record OrganizationAdminActiveShiftDto(
    Guid ShiftId,
    Guid OpenedByStaffUserId,
    DateTimeOffset OpenedAtUtc,
    MoneyDto ExpectedCash,
    bool IsProvisional);

public sealed record OrganizationAdminSummaryReportDto(
    OrganizationAdminReportPeriodDto Period,
    int AttentionTotalCount,
    IReadOnlyList<OrganizationAdminReportAttentionDto> AttentionItems,
    OrganizationAdminReportFiguresDto Figures,
    IReadOnlyList<OrganizationAdminRevenueTrendPointDto> Trend,
    OrganizationAdminActiveShiftDto? ActiveShift);

public sealed record OrganizationAdminShiftCashReportDto(
    OrganizationAdminReportPeriodDto Period,
    IReadOnlyList<ShiftReportRowDto> Shifts,
    IReadOnlyList<CashOperationReportRowDto> CashOperations,
    MoneyDto CashInTotal,
    MoneyDto CashOutTotal,
    MoneyDto NetCashTotal);

public sealed record OrganizationAdminRevenueComparisonDto(
    MoneyDto PreviousNetRevenue,
    long DifferenceMinorUnits,
    decimal? ChangePercent);

public sealed record OrganizationAdminRevenueSourceDto(string Source, MoneyDto Revenue);

public sealed record OrganizationAdminRevenueBreakdownDto(string Key, string Label, MoneyDto Revenue);

public sealed record OrganizationAdminRevenueReportDto(
    OrganizationAdminReportPeriodDto Period,
    MoneyDto GrossRevenue,
    MoneyDto Refunds,
    MoneyDto NetRevenue,
    MoneyDto GameplayRevenue,
    int GameplaySeconds,
    MoneyDto PosNetSales,
    OrganizationAdminRevenueComparisonDto Comparison,
    IReadOnlyList<OrganizationAdminRevenueSourceDto> Sources,
    IReadOnlyList<OrganizationAdminRevenueBreakdownDto> PaymentMethods,
    IReadOnlyList<OrganizationAdminRevenueBreakdownDto> Operators);
