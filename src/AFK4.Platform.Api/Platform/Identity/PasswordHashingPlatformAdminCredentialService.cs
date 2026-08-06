using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Identity;

public sealed class PasswordHashingPlatformAdminCredentialService(
    PlatformDbContext dbContext,
    PlatformAdminTwoFactorService twoFactorService) : IPlatformAdminCredentialService
{
    private readonly PasswordHasher<PlatformAdminUserEntity> passwordHasher = new();

    public async Task<PlatformAdminSignInChallengeResponse?> SignInAsync(
        PlatformAdminSignInRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        var normalizedUserName = request.UserName.Trim().ToUpperInvariant();
        var user = await dbContext.PlatformAdminUsers.SingleOrDefaultAsync(
            candidate => candidate.NormalizedUserName == normalizedUserName && candidate.IsActive,
            cancellationToken);

        if (user is null)
        {
            return null;
        }

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        return await twoFactorService.StartChallengeAsync(user, cancellationToken);
    }
}
