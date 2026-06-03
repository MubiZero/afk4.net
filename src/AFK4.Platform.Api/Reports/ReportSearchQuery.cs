namespace AFK4.Platform.Api.Reports;

public sealed record ReportSearchQuery(
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int? Limit,
    // Anti-fraud §5.5: filter the operator-actions report by who acted and by money-action amount —
    // "every refund Alice did this week over 50 TJS".
    Guid? ActorStaffUserId = null,
    long? MinAmountMinorUnits = null,
    long? MaxAmountMinorUnits = null);
