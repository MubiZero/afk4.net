using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Players;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Identity;

public sealed class PlayerCredentialService(
    PlatformDbContext dbContext,
    IPlayerTokenService tokenService,
    TimeProvider timeProvider) : IPlayerCredentialService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private readonly PasswordHasher<PlayerCredentialEntity> passwordHasher = new();

    public async Task<PlayerSignInResponse?> SignInAsync(
        PlayerSignInRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var account = await dbContext.PlayerAccounts.SingleOrDefaultAsync(
            p => p.OrganizationId == request.OrganizationId
                 && p.PhoneNumber == request.PhoneNumber
                 && p.IsActive,
            cancellationToken);
        if (account is null)
        {
            return null;
        }

        var credential = await dbContext.PlayerCredentials.SingleOrDefaultAsync(
            c => c.PlayerAccountId == account.PlayerAccountId, cancellationToken);
        if (credential?.PasswordHash is null)
        {
            return null;
        }

        if (credential.LockedUntilUtc is { } lockedUntil && lockedUntil > now)
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(
            credential, credential.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            credential.FailedLoginCount++;
            if (credential.FailedLoginCount >= MaxFailedAttempts)
            {
                credential.LockedUntilUtc = now.Add(LockoutDuration);
            }

            credential.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        credential.FailedLoginCount = 0;
        credential.LockedUntilUtc = null;
        credential.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await tokenService.IssueAsync(account, credential.PhoneVerified, cancellationToken);
    }

    public async Task SetPasswordAsync(
        Guid playerAccountId, string password, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var account = await dbContext.PlayerAccounts.SingleAsync(
            p => p.PlayerAccountId == playerAccountId, cancellationToken);

        var credential = await dbContext.PlayerCredentials.SingleOrDefaultAsync(
            c => c.PlayerAccountId == playerAccountId, cancellationToken);
        if (credential is null)
        {
            credential = new PlayerCredentialEntity
            {
                PlayerCredentialId = Guid.NewGuid(),
                PlayerAccountId = playerAccountId,
                OrganizationId = account.OrganizationId,
                CreatedAtUtc = now
            };
            dbContext.PlayerCredentials.Add(credential);
        }

        credential.PasswordHash = passwordHasher.HashPassword(credential, password);
        credential.FailedLoginCount = 0;
        credential.LockedUntilUtc = null;
        credential.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
