namespace AFK4.Shared.Contracts.Identity;

public sealed record OwnerCodeSummaryResponse(
    string CodeSuffix,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? LastUsedAtUtc,
    int FailedAttemptCount);
