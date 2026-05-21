namespace AFK4.Platform.Api.Dashboard;

public sealed record DashboardSummaryQuery(
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int? Limit);
