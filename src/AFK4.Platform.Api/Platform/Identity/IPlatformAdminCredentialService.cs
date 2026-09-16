using AFK4.Shared.Contracts.Platform.Auth;

namespace AFK4.Platform.Api.Platform.Identity;

/// <summary>
/// Исход проверки пароля. «Отказано» и «заперто» разведены намеренно: человеку, который заперт,
/// надо сказать про ожидание, а не про неверный пароль — иначе он будет менять раскладку и
/// пробовать снова, каждый раз продлевая запрет.
/// </summary>
public sealed record PlatformAdminSignInResult(
    PlatformAdminSignInChallengeResponse? Challenge,
    bool IsLockedOut)
{
    public static PlatformAdminSignInResult Started(PlatformAdminSignInChallengeResponse challenge) =>
        new(challenge, false);

    public static PlatformAdminSignInResult Rejected() => new(null, false);

    public static PlatformAdminSignInResult LockedOut() => new(null, true);
}

public interface IPlatformAdminCredentialService
{
    // Password check only — this issues a sign-in challenge, never a working session. The caller
    // must complete /auth/2fa/setup or /auth/2fa/verify with the returned ChallengeToken to obtain
    // a real PlatformAdminSignInResponse.
    Task<PlatformAdminSignInResult> SignInAsync(PlatformAdminSignInRequest request, CancellationToken cancellationToken);
}
