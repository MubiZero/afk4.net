using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Identity;

public sealed class PasswordHashingStaffCredentialService(
    PlatformDbContext dbContext,
    IStaffTokenService tokenService) : IStaffCredentialService
{
    private readonly PasswordHasher<StaffUserEntity> passwordHasher = new();

    public async Task<StaffSignInResponse?> SignInAsync(
        StaffSignInRequest request,
        CancellationToken cancellationToken)
    {
        if (request.OrganizationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.UserName) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        var normalizedUserName = request.UserName.Trim().ToUpperInvariant();
        var user = await dbContext.StaffUsers.SingleOrDefaultAsync(
            candidate =>
                candidate.OrganizationId == request.OrganizationId &&
                candidate.NormalizedUserName == normalizedUserName &&
                candidate.IsActive,
            cancellationToken);

        if (user is null)
        {
            return null;
        }

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        return result == PasswordVerificationResult.Failed
            ? null
            : await tokenService.IssueAsync(user, cancellationToken);
    }
}
