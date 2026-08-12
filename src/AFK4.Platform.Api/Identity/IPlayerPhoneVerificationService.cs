namespace AFK4.Platform.Api.Identity;

/// <summary>Where the player's phone stands right now: the number and whether it is confirmed.</summary>
public sealed record PlayerPhoneStatus(string? Phone, DateTimeOffset? PhoneVerifiedAtUtc);

/// <summary>
/// Confirming a player's phone by SMS code. Statuses are shared with the staff flow
/// (<see cref="PhoneVerificationStartStatus"/>, <see cref="PhoneConfirmStatus"/>): the rules are
/// the same, and two parallel enums would drift apart at the first change.
/// </summary>
public interface IPlayerPhoneVerificationService
{
    Task<PhoneVerificationStartResult> StartAsync(
        Guid playerAccountId, string rawPhone, CancellationToken cancellationToken);

    Task<PhoneConfirmResult> ConfirmAsync(
        Guid playerAccountId, string code, CancellationToken cancellationToken);

    Task<PlayerPhoneStatus> GetStatusAsync(Guid playerAccountId, CancellationToken cancellationToken);
}
