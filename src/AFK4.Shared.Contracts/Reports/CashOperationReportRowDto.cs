using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Reports;

public sealed record CashOperationReportRowDto(
    Guid OperationId,
    Guid OrganizationId,
    Guid BranchId,
    Guid? ShiftId,
    Guid CreatedByStaffUserId,
    string SourceType,
    string OperationType,
    MoneyDto CashImpact,
    string Reason,
    DateTimeOffset CreatedAtUtc);
