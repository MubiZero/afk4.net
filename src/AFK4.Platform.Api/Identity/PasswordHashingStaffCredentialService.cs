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

        var user = await ResolveOrgUserAsync(request.OrganizationId, request.UserName, cancellationToken);
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
        Guid organizationId,
        StaffSignInByLoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Login) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return StaffLoginResolution.None;
        }

        var normalizedLogin = request.Login.Trim().ToUpperInvariant();
        var loweredLogin = request.Login.Trim().ToLowerInvariant();
        var candidates = await dbContext.StaffUsers
            .AsNoTracking()
            .Where(candidate => candidate.OrganizationId == organizationId &&
                candidate.IsActive &&
                (candidate.NormalizedUserName == normalizedLogin ||
                 (candidate.Email != null && candidate.Email.ToLower() == loweredLogin)))
            .Select(candidate => new { candidate.OrganizationId, candidate.StaffUserId, candidate.PasswordHash })
            .ToListAsync(cancellationToken);

        var matched = new List<(Guid OrganizationId, Guid StaffUserId)>();
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
                matched.Add((candidate.OrganizationId, candidate.StaffUserId));
            }
        }

        var matchedOrgIds = matched.Select(entry => entry.OrganizationId).Distinct().ToList();

        if (matchedOrgIds.Count == 0)
        {
            return StaffLoginResolution.None;
        }

        if (matchedOrgIds.Count == 1)
        {
            // We already verified this exact account above. Issue the token directly
            // instead of re-resolving by login — re-resolution would pick the wrong
            // user when a username collides with another account's email.
            var staffUserId = matched.First(entry => entry.OrganizationId == matchedOrgIds[0]).StaffUserId;
            var user = await dbContext.StaffUsers
                .FirstOrDefaultAsync(candidate => candidate.StaffUserId == staffUserId, cancellationToken);
            if (user is null)
            {
                return StaffLoginResolution.None;
            }

            var signedIn = await tokenService.IssueAsync(user, cancellationToken);
            return new StaffLoginResolution(signedIn, Array.Empty<StaffSignInClubChoice>());
        }

        var clubs = await dbContext.Organizations
            .AsNoTracking()
            .Where(organization => matchedOrgIds.Contains(organization.OrganizationId))
            .Select(organization => new StaffSignInClubChoice(organization.OrganizationId, organization.Name))
            .ToListAsync(cancellationToken);
        return new StaffLoginResolution(null, clubs);
    }

    // Resolves an active staff user in the org by username first, then by email
    // (case-insensitive), then by verified phone. Username/email win on the pathological
    // collision; the phone branch only fires when the input parses as an international number,
    // so a normal login never accidentally matches a phone. Similar to
    // EfStaffPasswordResetService.ResolveStaffAsync, but org-scoped and IsActive-filtered.
    private async Task<StaffUserEntity?> ResolveOrgUserAsync(
        Guid organizationId, string loginOrEmail, CancellationToken cancellationToken)
    {
        var normalizedUserName = loginOrEmail.Trim().ToUpperInvariant();
        var byUserName = await dbContext.StaffUsers.SingleOrDefaultAsync(
            candidate =>
                candidate.OrganizationId == organizationId &&
                candidate.NormalizedUserName == normalizedUserName &&
                candidate.IsActive,
            cancellationToken);
        if (byUserName is not null)
        {
            return byUserName;
        }

        var loweredEmail = loginOrEmail.Trim().ToLowerInvariant();
        var byEmail = await dbContext.StaffUsers.FirstOrDefaultAsync(
            candidate =>
                candidate.OrganizationId == organizationId &&
                candidate.Email != null &&
                candidate.Email.ToLower() == loweredEmail &&
                candidate.IsActive,
            cancellationToken);
        if (byEmail is not null)
        {
            return byEmail;
        }

        // Phone-first sign-in (Operator/Wizard send the full E.164 digits as the login). Match only
        // a verified phone so an unconfirmed number can't be used to sign in — parity with the
        // dedicated SignInByPhoneAsync path.
        var normalizedPhone = PhoneNumberNormalizer.Normalize(loginOrEmail);
        if (normalizedPhone is null)
        {
            return null;
        }

        return await dbContext.StaffUsers.FirstOrDefaultAsync(
            candidate =>
                candidate.OrganizationId == organizationId &&
                candidate.NormalizedPhone == normalizedPhone &&
                candidate.PhoneVerifiedAtUtc != null &&
                candidate.IsActive,
            cancellationToken);
    }

    public async Task<StaffSignInResponse?> SignInByPhoneAsync(
        StaffSignInByPhoneRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        var normalizedPhone = PhoneNumberNormalizer.Normalize(request.PhoneNumber);
        if (normalizedPhone is null)
        {
            return null;
        }

        var user = await dbContext.StaffUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.NormalizedPhone == normalizedPhone
                    && candidate.PhoneVerifiedAtUtc != null
                    && candidate.IsActive,
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
