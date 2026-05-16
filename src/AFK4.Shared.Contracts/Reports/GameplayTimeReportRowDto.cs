using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Reports;

public sealed record GameplayTimeReportRowDto(
    Guid SessionId,
    Guid OrganizationId,
    Guid BranchId,
    Guid SeatId,
    Guid DeviceId,
    Guid CreatedByStaffUserId,
    string PlayerKind,
    Guid? PlayerAccountId,
    string State,
    int DurationSeconds,
    int PackageSeconds,
    int BonusSeconds,
    MoneyDto GameplayRevenue,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    DateTimeOffset? EndsAtUtc);
