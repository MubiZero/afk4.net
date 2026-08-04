using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Security;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Identity;

public enum TwoFactorError
{
    None,
    InvalidChallenge,
    InvalidCode,
    LockedOut,
    AlreadyConfigured
}

// Second-factor lifecycle for platform admins: sign-in challenges, TOTP enrollment, verification
// (TOTP or a one-time recovery code), and admin-initiated reset. Every method here is reachable
// either anonymously via a challenge token (setup/verify) or under ManagePlatformAdmins (reset) —
// none of it goes through the normal PlatformAdminAuthorizationService session path, by design.
public sealed class PlatformAdminTwoFactorService(
    PlatformDbContext dbContext,
    IPlatformAdminTokenService tokenService,
    ISecretProtector secretProtector,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private const int MaxFailedAttempts = 5;
    private const int TotpSecretByteLength = 20;
    private const int RecoveryCodeCount = 10;
    private const int RecoveryCodeByteLength = 5; // 5 bytes -> 10 hex chars

    public async Task<PlatformAdminSignInChallengeResponse> StartChallengeAsync(
        PlatformAdminUserEntity user,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var token = GenerateChallengeToken();
        var challenge = new PlatformAdminSignInChallengeEntity
        {
            ChallengeId = Guid.NewGuid(),
            PlatformAdminUserId = user.PlatformAdminUserId,
            TokenHash = HashText(token),
            ExpiresAtUtc = now.Add(ChallengeLifetime)
        };

        dbContext.PlatformAdminSignInChallenges.Add(challenge);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new PlatformAdminSignInChallengeResponse(token, challenge.ExpiresAtUtc, user.TotpEnabledAtUtc is not null);
    }

    public async Task<(string? Secret, string? OtpAuthUri, TwoFactorError Error)> BeginSetupAsync(
        string challengeToken,
        CancellationToken cancellationToken)
    {
        var (_, user) = await FindActiveChallengeAsync(challengeToken, cancellationToken);
        if (user is null)
        {
            return (null, null, TwoFactorError.InvalidChallenge);
        }

        if (user.TotpEnabledAtUtc is not null)
        {
            return (null, null, TwoFactorError.AlreadyConfigured);
        }

        var secretBytes = RandomNumberGenerator.GetBytes(TotpSecretByteLength);
        user.TotpSecretEncrypted = secretProtector.Protect(Convert.ToBase64String(secretBytes));
        await dbContext.SaveChangesAsync(cancellationToken);

        var base32Secret = TotpCodeGenerator.ToBase32(secretBytes);
        return (base32Secret, BuildOtpAuthUri(user.UserName, base32Secret), TwoFactorError.None);
    }

    // PlatformAdminUserId is returned separately from Session so the caller can attribute a Denied
    // audit entry to the account being probed even when the code was wrong — the whole point of an
    // auth audit trail is telling "someone typo'd" apart from "this specific account is being
    // brute-forced". It is populated whenever the challenge resolved to a real user, regardless of
    // whether the code check itself then succeeded.
    public async Task<(PlatformAdminSignInResponse? Session, IReadOnlyList<string> RecoveryCodes, Guid? PlatformAdminUserId, TwoFactorError Error)> CompleteSetupAsync(
        string challengeToken,
        string code,
        CancellationToken cancellationToken)
    {
        var (challenge, user) = await FindActiveChallengeAsync(challengeToken, cancellationToken);
        if (challenge is null || user is null)
        {
            return (null, [], null, TwoFactorError.InvalidChallenge);
        }

        if (user.TotpEnabledAtUtc is not null)
        {
            return (null, [], user.PlatformAdminUserId, TwoFactorError.AlreadyConfigured);
        }

        if (string.IsNullOrWhiteSpace(user.TotpSecretEncrypted))
        {
            // No prior call to BeginSetupAsync produced a pending secret to confirm.
            return (null, [], user.PlatformAdminUserId, TwoFactorError.InvalidChallenge);
        }

        var now = timeProvider.GetUtcNow();
        var secretBytes = Convert.FromBase64String(secretProtector.Unprotect(user.TotpSecretEncrypted));
        if (!TotpCodeGenerator.Verify(secretBytes, code, now.ToUnixTimeSeconds()))
        {
            return (null, [], user.PlatformAdminUserId, TwoFactorError.InvalidCode);
        }

        var recoveryCodes = GenerateRecoveryCodes();
        user.RecoveryCodeHashesJson = SerializeRecoveryCodeHashes(recoveryCodes);
        user.TotpEnabledAtUtc = now;
        user.LastSignInAtUtc = now;
        user.FailedTwoFactorAttempts = 0;
        user.TwoFactorLockedUntilUtc = null;
        challenge.ConsumedAtUtc = now;

        // IssueAsync persists its own new token entities via SaveChangesAsync on this same tracked
        // context, which also flushes the user/challenge mutations above in the same round trip.
        var session = await tokenService.IssueAsync(user, cancellationToken);

        return (session, recoveryCodes, user.PlatformAdminUserId, TwoFactorError.None);
    }

    // See CompleteSetupAsync above for why PlatformAdminUserId travels separately from Session.
    public async Task<(PlatformAdminSignInResponse? Session, Guid? PlatformAdminUserId, TwoFactorError Error)> VerifyAsync(
        string challengeToken,
        string code,
        CancellationToken cancellationToken)
    {
        var (challenge, user) = await FindActiveChallengeAsync(challengeToken, cancellationToken);
        if (challenge is null || user is null)
        {
            return (null, null, TwoFactorError.InvalidChallenge);
        }

        var now = timeProvider.GetUtcNow();
        if (user.TwoFactorLockedUntilUtc is { } lockedUntil && lockedUntil > now)
        {
            return (null, user.PlatformAdminUserId, TwoFactorError.LockedOut);
        }

        var succeeded = false;
        if (!string.IsNullOrWhiteSpace(user.TotpSecretEncrypted))
        {
            var secretBytes = Convert.FromBase64String(secretProtector.Unprotect(user.TotpSecretEncrypted));
            succeeded = TotpCodeGenerator.Verify(secretBytes, code, now.ToUnixTimeSeconds());
        }

        if (!succeeded)
        {
            var hashes = ParseRecoveryCodeHashes(user.RecoveryCodeHashesJson);
            var candidateHash = HashRecoveryCodeHex(code);
            if (hashes.Remove(candidateHash))
            {
                succeeded = true;
                user.RecoveryCodeHashesJson = JsonSerializer.Serialize(hashes);
            }
        }

        if (!succeeded)
        {
            user.FailedTwoFactorAttempts++;
            if (user.FailedTwoFactorAttempts >= MaxFailedAttempts)
            {
                user.TwoFactorLockedUntilUtc = now.Add(LockoutDuration);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return (null, user.PlatformAdminUserId, TwoFactorError.InvalidCode);
        }

        user.FailedTwoFactorAttempts = 0;
        user.TwoFactorLockedUntilUtc = null;
        user.LastSignInAtUtc = now;
        challenge.ConsumedAtUtc = now;

        var session = await tokenService.IssueAsync(user, cancellationToken);
        return (session, user.PlatformAdminUserId, TwoFactorError.None);
    }

    public async Task<TwoFactorError> ResetAsync(Guid targetPlatformAdminUserId, CancellationToken cancellationToken)
    {
        var user = await dbContext.PlatformAdminUsers.SingleOrDefaultAsync(
            candidate => candidate.PlatformAdminUserId == targetPlatformAdminUserId,
            cancellationToken);

        if (user is null)
        {
            return TwoFactorError.None;
        }

        user.TotpSecretEncrypted = null;
        user.TotpEnabledAtUtc = null;
        user.RecoveryCodeHashesJson = "[]";
        user.FailedTwoFactorAttempts = 0;
        user.TwoFactorLockedUntilUtc = null;

        await dbContext.SaveChangesAsync(cancellationToken);
        return TwoFactorError.None;
    }

    // Looks up a not-yet-consumed, not-yet-expired challenge by its hashed token and the still-active
    // user behind it. Candidates are filtered by the cheap predicates first, then compared by hash
    // client-side — same pattern as invitation code lookup, avoids relying on provider-specific
    // byte[] equality translation.
    private async Task<(PlatformAdminSignInChallengeEntity? Challenge, PlatformAdminUserEntity? User)> FindActiveChallengeAsync(
        string challengeToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(challengeToken))
        {
            return (null, null);
        }

        var now = timeProvider.GetUtcNow();
        var tokenHash = HashText(challengeToken);
        var candidates = await dbContext.PlatformAdminSignInChallenges
            .Where(candidate => candidate.ConsumedAtUtc == null && candidate.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);
        var challenge = candidates.SingleOrDefault(candidate => candidate.TokenHash.SequenceEqual(tokenHash));

        if (challenge is null)
        {
            return (null, null);
        }

        var user = await dbContext.PlatformAdminUsers.SingleOrDefaultAsync(
            candidate => candidate.PlatformAdminUserId == challenge.PlatformAdminUserId && candidate.IsActive,
            cancellationToken);

        return user is null ? (null, null) : (challenge, user);
    }

    private static string BuildOtpAuthUri(string userName, string base32Secret)
    {
        var label = Uri.EscapeDataString($"AFK4:{userName}");
        return $"otpauth://totp/{label}?secret={base32Secret}&issuer=AFK4&digits=6&period=30";
    }

    private static List<string> GenerateRecoveryCodes()
    {
        var codes = new List<string>(RecoveryCodeCount);
        for (var i = 0; i < RecoveryCodeCount; i++)
        {
            codes.Add(Convert.ToHexString(RandomNumberGenerator.GetBytes(RecoveryCodeByteLength)));
        }

        return codes;
    }

    private static string SerializeRecoveryCodeHashes(IEnumerable<string> plainCodes) =>
        JsonSerializer.Serialize(plainCodes.Select(HashRecoveryCodeHex));

    private static List<string> ParseRecoveryCodeHashes(string recoveryCodeHashesJson)
    {
        if (string.IsNullOrWhiteSpace(recoveryCodeHashesJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(recoveryCodeHashesJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string HashRecoveryCodeHex(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant())));

    private static string GenerateChallengeToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static byte[] HashText(string text) => SHA256.HashData(Encoding.UTF8.GetBytes(text));
}
