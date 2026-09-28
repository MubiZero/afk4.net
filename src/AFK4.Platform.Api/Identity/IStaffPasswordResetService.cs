namespace AFK4.Platform.Api.Identity;

public enum ResetPasswordByEmailStatus
{
    Success,
    InvalidCode,
    Expired,
    NoActiveCode,
    TooManyAttempts,
}

public sealed record ResetPasswordByEmailResult(
    ResetPasswordByEmailStatus Status,
    int RemainingAttempts);
