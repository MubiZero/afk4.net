namespace AFK4.Shared.Contracts.Reports;

/// <summary>Creates a recurring report delivery for a branch. <paramref name="ReportType"/> is one of
/// <see cref="ScheduledReportTypeNames"/>; <paramref name="Frequency"/> one of
/// <see cref="ReportScheduleFrequencyNames"/>.</summary>
public sealed record CreateReportScheduleRequest(Guid OrganizationId, string ReportType, string Frequency);

/// <summary>A configured report schedule.</summary>
public sealed record ReportScheduleDto(
    Guid ReportScheduleId,
    Guid OrganizationId,
    Guid BranchId,
    string ReportType,
    string Frequency,
    bool IsActive,
    DateTimeOffset NextRunUtc,
    DateTimeOffset? LastRunUtc,
    DateTimeOffset CreatedAtUtc);
