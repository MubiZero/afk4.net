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

public interface IStaffPhonePasswordResetService
{
    /// <summary>
    /// Sends an SMS reset code to the verified phone if it maps to an active staff account. The
    /// result is uniform whether or not an account exists (anti-enumeration); only a malformed
    /// phone yields <see cref="ForgotPasswordByPhoneStatus.InvalidPhone"/>.
    /// </summary>
    Task<ForgotPasswordByPhoneResult> RequestResetAsync(string rawPhone, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies the SMS code for the phone and, on success, sets the new password and revokes the
    /// account's active tokens. A missing account/code collapses to
    /// <see cref="ResetPasswordByPhoneStatus.NoActiveCode"/> (no enumeration).
    /// </summary>
    Task<ResetPasswordByPhoneResult> ResetAsync(
        string rawPhone, string code, string newPassword, CancellationToken cancellationToken);
}
