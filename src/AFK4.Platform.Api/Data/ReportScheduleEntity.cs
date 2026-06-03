namespace AFK4.Platform.Api.Data;

/// <summary>
/// A recurring report delivery: the scheduled-report runner picks up active schedules whose
/// <see cref="NextRunUtc"/> is due, generates the CSV for the prior window, emails it to the org
/// owner as an attachment, then advances <see cref="NextRunUtc"/> by the frequency. (Stage 5 digest.)
/// </summary>
public sealed class ReportScheduleEntity
{
    public Guid ReportScheduleId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    /// <summary>One of <c>ScheduledReportTypeNames</c>.</summary>
    public string ReportType { get; set; } = string.Empty;

    /// <summary>One of <c>ReportScheduleFrequencyNames</c>.</summary>
    public string Frequency { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset NextRunUtc { get; set; }

    public DateTimeOffset? LastRunUtc { get; set; }

    public Guid CreatedByStaffUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
