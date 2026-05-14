using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Reports;

public sealed record ShiftReportRowDto(
    Guid ShiftId,
    Guid OrganizationId,
    Guid BranchId,
    Guid OpenedByStaffUserId,
    Guid? ClosedByStaffUserId,
    string State,
    MoneyDto StartingCash,
    MoneyDto CashMovementsTotal,
    MoneyDto PosCashPaymentsTotal,
    MoneyDto PosRefundsTotal,
    MoneyDto BillingCashImpactTotal,
    MoneyDto ExpectedCash,
    MoneyDto? CountedCash,
    MoneyDto? Difference,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset? ClosedAtUtc);
