using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Identity;

public sealed class PasswordHashingPlatformAdminCredentialService(
    PlatformDbContext dbContext,
    PlatformAdminTwoFactorService twoFactorService,
    TimeProvider timeProvider) : IPlatformAdminCredentialService
{
    // Те же пять попыток и те же пятнадцать минут, что у второго фактора: одна дверь — один
    // порядок. Порог не про удобство подбора, а про то, чтобы человек, промахнувшийся раскладкой,
    // успел исправиться.
    private const int MaxFailedAttempts = 5;

    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly PasswordHasher<PlatformAdminUserEntity> passwordHasher = new();

    public async Task<PlatformAdminSignInResult> SignInAsync(
        PlatformAdminSignInRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return PlatformAdminSignInResult.Rejected();
        }

        var normalizedUserName = request.UserName.Trim().ToUpperInvariant();
        var user = await dbContext.PlatformAdminUsers.SingleOrDefaultAsync(
            candidate => candidate.NormalizedUserName == normalizedUserName && candidate.IsActive,
            cancellationToken);

        if (user is null)
        {
            // Несуществующий логин отвечает тем же «неверно», что и неверный пароль: иначе перебор
            // сначала находит живые логины, а уже потом подбирает к ним пароль.
            return PlatformAdminSignInResult.Rejected();
        }

        var now = timeProvider.GetUtcNow();
        if (user.PasswordLockedUntilUtc is { } lockedUntil && lockedUntil > now)
        {
            return PlatformAdminSignInResult.LockedOut();
        }

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            user.FailedPasswordAttempts++;
            var lockedOut = user.FailedPasswordAttempts >= MaxFailedAttempts;
            if (lockedOut)
            {
                user.PasswordLockedUntilUtc = now.Add(LockoutDuration);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return lockedOut ? PlatformAdminSignInResult.LockedOut() : PlatformAdminSignInResult.Rejected();
        }

        if (user.FailedPasswordAttempts != 0 || user.PasswordLockedUntilUtc is not null)
        {
            // Верный пароль снимает счётчик: иначе пять промахов за месяц однажды запрут того, кто
            // каждый раз заходил успешно.
            user.FailedPasswordAttempts = 0;
            user.PasswordLockedUntilUtc = null;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var challenge = await twoFactorService.StartChallengeAsync(user, cancellationToken);
        return PlatformAdminSignInResult.Started(challenge);
    }
}
