using AFK4.Shared.Contracts.Players;

namespace AFK4.Platform.Api.Identity;

public enum PlayerCodeSignInStatus
{
    SignedIn,
    InvalidCode,
    Expired,
    NoActiveCode,
    TooManyAttempts,
}

public sealed record PlayerCodeSignInResult(
    PlayerCodeSignInStatus Status,
    PlayerSignInResponse? Tokens,
    int RemainingAttempts);

/// <summary>
/// Signing in with an SMS code instead of a PIN — the phone is the player's login already, so a
/// code proves the same thing a password does without a password to forget.
/// </summary>
public interface IPlayerPhoneSignInService
{
    /// <summary>
    /// Sends a sign-in code. Always reports the same timings whether or not the number belongs to
    /// a player: a different answer would turn this endpoint into a directory of who plays where.
    /// </summary>
    Task<PhoneVerificationStartResult> StartAsync(
        Guid organizationId, string rawPhone, CancellationToken cancellationToken);

    Task<PlayerCodeSignInResult> ConfirmAsync(
        Guid organizationId, string rawPhone, string code, CancellationToken cancellationToken);
}
