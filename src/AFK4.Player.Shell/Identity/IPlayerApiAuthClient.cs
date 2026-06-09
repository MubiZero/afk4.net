namespace AFK4.Player.Shell.Identity;

public readonly record struct AuthSnapshot(bool Authenticated, string? DisplayName, bool PhoneVerified);

public interface IPlayerApiAuthClient
{
    AuthSnapshot Current { get; }
    string? CurrentAccessToken { get; }
    Task<AuthSnapshot> SignInAsync(Guid organizationId, string phoneNumber, string password, CancellationToken ct);
    Task EnsureFreshTokenAsync(CancellationToken ct);
    void SignOut();
}
