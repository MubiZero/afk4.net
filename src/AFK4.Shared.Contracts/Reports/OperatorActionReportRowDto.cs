namespace AFK4.Shared.Contracts.Reports;

public sealed record OperatorActionReportRowDto(
    Guid? ActorStaffUserId,
    string ActorDisplayName,
    string Action,
    string Outcome,
    int Count,
    DateTimeOffset FirstAtUtc,
    DateTimeOffset LastAtUtc);
