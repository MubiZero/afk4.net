namespace AFK4.Platform.Api.Reports;

public sealed record ReportSearchQuery(
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int? Limit);
