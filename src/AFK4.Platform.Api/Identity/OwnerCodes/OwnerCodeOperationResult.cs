namespace AFK4.Platform.Api.Identity.OwnerCodes;

public enum OwnerCodeOperationStatus
{
    Succeeded,
    NotFound,
    Conflict
}

public sealed record OwnerCodeOperationResult<T>(
    OwnerCodeOperationStatus Status,
    T? Value,
    string? Error)
    where T : class
{
    public bool Succeeded => Status == OwnerCodeOperationStatus.Succeeded;

    public static OwnerCodeOperationResult<T> Success(T value) =>
        new(OwnerCodeOperationStatus.Succeeded, value, null);

    public static OwnerCodeOperationResult<T> Conflict(string error) =>
        new(OwnerCodeOperationStatus.Conflict, null, error);

    public static OwnerCodeOperationResult<T> NotFound(string error) =>
        new(OwnerCodeOperationStatus.NotFound, null, error);
}

public sealed record OwnerCodeIssued(
    string PlaintextCode,
    string CodeSuffix,
    DateTimeOffset ExpiresAtUtc);

public sealed record OwnerCodeSummary(
    string CodeSuffix,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? LastUsedAtUtc,
    int FailedAttemptCount);
