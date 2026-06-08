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

/// <summary>
/// Self-service password reset for staff/owner accounts (owners are staff users). The email channel
/// now mirrors the SMS channel: a short 6-digit code is emailed, then entered (with the account's
/// login/email) to set a new password — with a per-code attempt counter. Anti-enumeration:
/// <see cref="RequestResetAsync"/> never reveals whether an account exists.
/// </summary>
public interface IStaffPasswordResetService
{
    /// <summary>
    /// Resolve the staff/owner by username or email; if found with a contact email, issue a 6-digit
    /// code and email it. Always completes without signalling whether the account exists.
    /// </summary>
    Task RequestResetAsync(string userNameOrEmail, CancellationToken cancellationToken);

    /// <summary>
    /// Verify the emailed code for the account resolved from <paramref name="userNameOrEmail"/>, set
    /// the new password hash, and revoke active sessions. Reports invalid/expired/too-many-attempts
    /// with remaining attempts so the UI can guide the user (parity with the SMS reset).
    /// </summary>
    Task<ResetPasswordByEmailResult> ResetAsync(
        string userNameOrEmail, string code, string newPassword, CancellationToken cancellationToken);
}
