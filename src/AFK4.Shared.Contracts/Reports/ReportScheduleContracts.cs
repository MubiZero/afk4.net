namespace AFK4.Shared.Contracts.Reports;

/// <summary>Creates a recurring report delivery for a branch. <paramref name="ReportType"/> is one of
/// <see cref="ScheduledReportTypeNames"/>; <paramref name="Frequency"/> one of
/// <see cref="ReportScheduleFrequencyNames"/>.</summary>
public sealed record CreateReportScheduleRequest(Guid OrganizationId, string ReportType, string Frequency);

/// <summary>
/// Правка рассылки: частота и пауза. Оба поля необязательны — присланное меняется,
/// пропущенное остаётся как было.
///
/// Пауза, а не удаление: уйти в отпуск на две недели и не получать письма — не то же самое,
/// что отказаться от рассылки совсем и заводить её заново.
/// </summary>
public sealed record UpdateReportScheduleRequest(
    Guid OrganizationId,
    string? Frequency = null,
    bool? IsActive = null);

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
