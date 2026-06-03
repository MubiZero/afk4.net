namespace AFK4.Platform.Api.Audit;

public sealed record AuditSearchQuery(
    string? Action,
    string? Outcome,
    string? TargetType,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int? Limit,
    // Anti-fraud §5.5: review by actor and amount range — "every refund Alice did over 50 TJS".
    Guid? ActorStaffUserId = null,
    long? MinAmountMinorUnits = null,
    long? MaxAmountMinorUnits = null);
