using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Tenancy;
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

    public async Task<StaffSignInResponse?> SignInByTenantKeyAsync(
        StaffSignInByTenantKeyRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenantKey) ||
            string.IsNullOrWhiteSpace(request.UserName) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        var tenantKey = SlugValidator.Normalize(request.TenantKey);
        if (Guid.TryParse(tenantKey, out _) ||
            SlugValidator.Validate(tenantKey, nameof(request.TenantKey)) is not null)
        {
            return null;
        }

        var organizationId = await dbContext.Organizations
            .AsNoTracking()
            .Where(candidate => candidate.Slug == tenantKey)
            .Select(candidate => (Guid?)candidate.OrganizationId)
            .SingleOrDefaultAsync(cancellationToken);

        return organizationId is null
            ? null
            : await SignInAsync(
                new StaffSignInRequest(organizationId.Value, request.UserName, request.Password),
                cancellationToken);
    }

    public async Task<StaffLoginResolution> SignInByLoginAsync(
        StaffSignInByLoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Login) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return StaffLoginResolution.None;
        }

        var normalizedLogin = request.Login.Trim().ToUpperInvariant();
        var candidates = await dbContext.StaffUsers
            .AsNoTracking()
            .Where(candidate => candidate.NormalizedUserName == normalizedLogin && candidate.IsActive)
            .Select(candidate => new { candidate.OrganizationId, candidate.StaffUserId, candidate.PasswordHash })
            .ToListAsync(cancellationToken);

        var matchedOrgIds = new List<Guid>();
        foreach (var candidate in candidates)
        {
            // VerifyHashedPassword only reads the hash; the entity is a placeholder.
            var placeholder = new StaffUserEntity
            {
                StaffUserId = candidate.StaffUserId,
                OrganizationId = candidate.OrganizationId
            };
            var result = passwordHasher.VerifyHashedPassword(placeholder, candidate.PasswordHash, request.Password);
            if (result != PasswordVerificationResult.Failed)
            {
                matchedOrgIds.Add(candidate.OrganizationId);
            }
        }

        if (matchedOrgIds.Count == 0)
        {
            return StaffLoginResolution.None;
        }

        if (matchedOrgIds.Count == 1)
        {
            var signedIn = await SignInAsync(
                new StaffSignInRequest(matchedOrgIds[0], request.Login, request.Password),
                cancellationToken);
            return new StaffLoginResolution(signedIn, Array.Empty<StaffSignInClubChoice>());
        }

        var clubs = await dbContext.Organizations
            .AsNoTracking()
            .Where(organization => matchedOrgIds.Contains(organization.OrganizationId))
            .Select(organization => new StaffSignInClubChoice(organization.OrganizationId, organization.Name))
            .ToListAsync(cancellationToken);
        return new StaffLoginResolution(null, clubs);
    }
}
