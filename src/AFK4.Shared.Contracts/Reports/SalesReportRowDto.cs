using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Reports;

public sealed record SalesReportRowDto(
    Guid PosSaleId,
    Guid OrganizationId,
    Guid BranchId,
    Guid ShiftId,
    Guid CreatedByStaffUserId,
    string State,
    MoneyDto Total,
    MoneyDto PaidAmount,
    MoneyDto RefundAmount,
    int LineCount,
    int ItemQuantity,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? PaidAtUtc,
    DateTimeOffset? RefundedAtUtc,
    DateTimeOffset? VoidedAtUtc);
