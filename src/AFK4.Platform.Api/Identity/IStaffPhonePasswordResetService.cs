namespace AFK4.Platform.Api.Identity;

public enum ForgotPasswordByPhoneStatus
{
    /// <summary>Request accepted. Uniform regardless of whether the phone maps to an account (anti-enumeration).</summary>
    Accepted,
    /// <summary>The supplied string is not a normalizable E.164 phone number.</summary>
    InvalidPhone,
}

public sealed record ForgotPasswordByPhoneResult(
    ForgotPasswordByPhoneStatus Status,
    int ExpiresInSeconds,
    int ResendAfterSeconds);

public enum ResetPasswordByPhoneStatus
{
    Success,
    InvalidCode,
    Expired,
    NoActiveCode,
    TooManyAttempts,
}

public sealed record ResetPasswordByPhoneResult(
    ResetPasswordByPhoneStatus Status,
    int RemainingAttempts);
