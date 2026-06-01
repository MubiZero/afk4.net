namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Picks up active <c>ReportScheduleEntity</c> rows whose <c>NextRunUtc</c> is due, generates the CSV
/// for the prior window via the report service + <c>ReportCsvExporter</c>, emails it to the org owner
/// as a Digest attachment (idempotent per (schedule, window)), and advances the schedule. Returns the
/// number of reports enqueued. (Stage 5 digest trigger.)
/// </summary>
public interface IScheduledReportRunner
{
    Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
