namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Builds the per-organization daily owner summary digest (revenue, shifts, discrepancies) for the
/// UTC day that ended before <paramref name="now"/> and enqueues one <c>owner.daily_summary</c>
/// notification per organization that had activity. Idempotent per (organization, date), so the
/// daily hosted service may tick repeatedly without re-sending. Returns the number of summaries
/// enqueued. (Stage 5 digest trigger.)
/// </summary>
public interface IDailySummaryRunner
{
    Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
