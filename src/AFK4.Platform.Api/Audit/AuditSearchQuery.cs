namespace AFK4.Platform.Api.Audit;

public sealed record AuditSearchQuery(
    string? Action,
    string? Outcome,
    string? TargetType,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int? Limit);
