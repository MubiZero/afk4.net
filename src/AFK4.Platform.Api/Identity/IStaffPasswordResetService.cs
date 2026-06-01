namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Self-service password reset for staff/owner accounts (owners are staff users). Complements the
/// operator-driven admin reset. Anti-enumeration: <see cref="RequestResetAsync"/> never reveals
/// whether an account exists. Tokens are single-use and expiring.
/// </summary>
public interface IStaffPasswordResetService
{
    /// <summary>
    /// Resolve the staff/owner by username or email; if found with a contact email, issue a reset
    /// token and email it. Always completes without signalling whether the account exists.
    /// </summary>
    Task RequestResetAsync(string userNameOrEmail, CancellationToken cancellationToken);

    /// <summary>
    /// Consume a reset token (single-use, must be unexpired), set the new password hash, and revoke
    /// the account's active sessions. Returns false when the token is unknown/expired/already used.
    /// </summary>
    Task<bool> CompleteResetAsync(string token, string newPassword, CancellationToken cancellationToken);
}
