using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Tenancy;
using AFK4.Shared.Contracts.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Identity;

public sealed class PasswordHashingStaffCredentialService(
    PlatformDbContext dbContext,
    IStaffTokenService tokenService,
    TimeProvider timeProvider) : IStaffCredentialService
{
    // Те же пять попыток и те же пятнадцать минут, что у администратора платформы. Порог не про
    // подбор, а про человека, промахнувшегося раскладкой: исправиться он успевает.
    private const int MaxFailedAttempts = 5;

    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly PasswordHasher<StaffUserEntity> passwordHasher = new();

    public async Task<StaffSignInOutcome> SignInAsync(
        StaffSignInRequest request,
        CancellationToken cancellationToken)
    {
        if (request.OrganizationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.UserName) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return StaffSignInOutcome.Rejected;
        }

        var user = await ResolveOrgUserAsync(request.OrganizationId, request.UserName, cancellationToken);
        if (user is null)
        {
            return StaffSignInOutcome.Rejected;
        }

        return await VerifyAndIssueAsync(user, request.Password, cancellationToken);
    }

    /// <summary>
    /// Проверить пароль и выдать вход, считая промахи.
    ///
    /// Без счёта промахов короткий пароль перебирается за часы: ограничение частоты стоит на
    /// адресе, а адресов у перебирающего столько, сколько он захочет купить. Счёт живёт на самой
    /// учётной записи, поэтому от смены адреса не спасает.
    /// </summary>
    private async Task<StaffSignInOutcome> VerifyAndIssueAsync(
        StaffUserEntity user,
        string password,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (user.PasswordLockedUntilUtc is { } lockedUntil && lockedUntil > now)
        {
            return StaffSignInOutcome.Locked;
        }

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            user.FailedPasswordAttempts++;
            var lockedOut = user.FailedPasswordAttempts >= MaxFailedAttempts;
            if (lockedOut)
            {
                user.PasswordLockedUntilUtc = now.Add(LockoutDuration);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return lockedOut ? StaffSignInOutcome.Locked : StaffSignInOutcome.Rejected;
        }

        if (user.FailedPasswordAttempts != 0 || user.PasswordLockedUntilUtc is not null)
        {
            // Верный пароль снимает счёт: иначе пять промахов за месяц однажды запрут того, кто
            // каждый раз заходил успешно.
            user.FailedPasswordAttempts = 0;
            user.PasswordLockedUntilUtc = null;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return StaffSignInOutcome.Success(await tokenService.IssueAsync(user, cancellationToken));
    }

    public async Task<StaffSignInOutcome> SignInByOrganizationKeyAsync(
        StaffSignInByOrganizationKeyRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OrganizationKey) ||
            string.IsNullOrWhiteSpace(request.UserName) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return StaffSignInOutcome.Rejected;
        }

        var organizationKey = SlugValidator.Normalize(request.OrganizationKey);
        if (Guid.TryParse(organizationKey, out _) ||
            SlugValidator.Validate(organizationKey, nameof(request.OrganizationKey)) is not null)
        {
            return StaffSignInOutcome.Rejected;
        }

        var organizationId = await dbContext.Organizations
            .AsNoTracking()
            .Where(candidate => candidate.Slug == organizationKey)
            .Select(candidate => (Guid?)candidate.OrganizationId)
            .SingleOrDefaultAsync(cancellationToken);

        return organizationId is null
            ? StaffSignInOutcome.Rejected
            : await SignInAsync(
                new StaffSignInRequest(organizationId.Value, request.UserName, request.Password),
                cancellationToken);
    }

    public async Task<StaffLoginResolution> SignInByLoginAsync(
        Guid? organizationId,
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
        // Не AsNoTracking: промахи по паролю здесь записываются на учётные записи.
        var candidates = await dbContext.StaffUsers
            // organizationId == null — вход из мастера установки, где организация ещё не
            // известна. Перебором это не становится: в matched попадают только те записи,
            // для которых пароль уже сошёлся, поэтому наружу уходят имена клубов, где эта же
            // пара логин/пароль и так работает.
            .Where(candidate => (organizationId == null || candidate.OrganizationId == organizationId) &&
                candidate.IsActive &&
                (candidate.NormalizedUserName == normalizedLogin ||
                 (candidate.Email != null && candidate.Email.ToLower() == loweredLogin)))
            .ToListAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        var matched = new List<(Guid OrganizationId, Guid StaffUserId)>();
        var anyLocked = false;
        var changed = false;
        foreach (var candidate in candidates)
        {
            // Запертую учётную запись даже не проверяем: иначе пятнадцать минут ожидания можно
            // было бы пересидеть, заходя этим же логином из мастера.
            if (candidate.PasswordLockedUntilUtc is { } lockedUntil && lockedUntil > now)
            {
                anyLocked = true;
                continue;
            }

            var result = passwordHasher.VerifyHashedPassword(candidate, candidate.PasswordHash, request.Password);
            if (result != PasswordVerificationResult.Failed)
            {
                matched.Add((candidate.OrganizationId, candidate.StaffUserId));
                if (candidate.FailedPasswordAttempts != 0 || candidate.PasswordLockedUntilUtc is not null)
                {
                    candidate.FailedPasswordAttempts = 0;
                    candidate.PasswordLockedUntilUtc = null;
                    changed = true;
                }

                continue;
            }

            candidate.FailedPasswordAttempts++;
            changed = true;
            if (candidate.FailedPasswordAttempts >= MaxFailedAttempts)
            {
                candidate.PasswordLockedUntilUtc = now.Add(LockoutDuration);
                anyLocked = true;
            }
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var matchedOrgIds = matched.Select(entry => entry.OrganizationId).Distinct().ToList();

        if (matchedOrgIds.Count == 0)
        {
            return anyLocked ? StaffLoginResolution.Locked : StaffLoginResolution.None;
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

    public async Task<StaffSignInOutcome> SignInByPhoneAsync(
        StaffSignInByPhoneRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return StaffSignInOutcome.Rejected;
        }

        var normalizedPhone = PhoneNumberNormalizer.Normalize(request.PhoneNumber);
        if (normalizedPhone is null)
        {
            return StaffSignInOutcome.Rejected;
        }

        // Не AsNoTracking: промах по паролю здесь записывается на учётную запись.
        var user = await dbContext.StaffUsers
            .FirstOrDefaultAsync(
                candidate => candidate.NormalizedPhone == normalizedPhone
                    && candidate.PhoneVerifiedAtUtc != null
                    && candidate.IsActive,
                cancellationToken);

        return user is null
            ? StaffSignInOutcome.Rejected
            : await VerifyAndIssueAsync(user, request.Password, cancellationToken);
    }
}
