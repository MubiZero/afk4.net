using AFK4.Shared.Contracts.Identity;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity.PhoneOtp;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Приглашение сотрудника по номеру телефона. Руководитель называет номер и роли и видит
/// шестизначный код первого входа; SMS его дублирует. Человек вводит свой номер, код и придумывает
/// себе ПИН — и получает счёт с уже подтверждённым телефоном, то есть входит номером, как все.
///
/// Пороги те же, что у сброса пароля по телефону: код шестизначный, живёт сутки, умирает после
/// трёх неверных попыток. Второе приглашение на тот же номер гасит первое — иначе отозвать
/// ошибочное приглашение было бы нечем.
/// </summary>
public sealed class EfStaffInviteService(
    PlatformDbContext db,
    INotificationService notifications,
    IPhoneOtpGenerator codeGenerator,
    IPhoneOtpHasher codeHasher,
    TimeProvider timeProvider,
    IOptions<NotificationOptions> options,
    IPlanLimitGuard planLimitGuard) : IStaffInviteService
{
    /// <summary>Сутки, а не неделя: шесть цифр, живущих неделю, перебираются спокойно.</summary>
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromHours(24);

    internal const int MaxAttempts = 3;

    private static readonly char[] RoleSeparator = [','];

    private readonly PasswordHasher<StaffUserEntity> passwordHasher = new();
    private readonly NotificationOptions options = options.Value;

    public async Task<StaffInviteCreateResult> CreateInviteAsync(
        Guid organizationId,
        Guid branchId,
        string userName,
        string displayName,
        string phoneNumber,
        string? email,
        IReadOnlyList<string> roleNames,
        CancellationToken cancellationToken)
    {
        var normalizedPhone = PhoneNumberNormalizer.Normalize(phoneNumber);
        if (normalizedPhone is null)
        {
            return StaffInviteCreateResult.Failed(
                "A valid phone number is required to send the invite.",
                StaffInviteErrorCodeNames.InvalidPhone);
        }

        var normalizedUserName = userName.Trim().ToUpperInvariant();
        var alreadyExists = await db.StaffUsers.AnyAsync(
            user => user.OrganizationId == organizationId && user.NormalizedUserName == normalizedUserName,
            cancellationToken);
        if (alreadyExists)
        {
            return StaffInviteCreateResult.Failed(
                "A staff user with this username already exists in the organization.",
                StaffInviteErrorCodeNames.UserNameTaken);
        }

        // Номер — глобальный вход сотрудника, и второй счёт на тот же номер сделал бы вход
        // неоднозначным. Приглашать человека, который уже работает, незачем: ему меняют роли.
        var phoneTaken = await db.StaffUsers.AnyAsync(
            user => user.NormalizedPhone == normalizedPhone
                && user.PhoneVerifiedAtUtc != null
                && user.IsActive,
            cancellationToken);
        if (phoneTaken)
        {
            return StaffInviteCreateResult.Failed(
                "This phone number already belongs to a staff member.",
                StaffInviteErrorCodeNames.PhoneTaken);
        }

        var planLimit = await planLimitGuard.CheckStaffUserAsync(organizationId, branchId, cancellationToken);
        if (planLimit is not null)
        {
            return StaffInviteCreateResult.PlanLimitReached(planLimit);
        }

        // Приглашали заново — старое гасим здесь же: два живых кода на один номер означают, что
        // отозвать ошибочное приглашение нечем.
        var previous = await db.StaffInvites
            .Where(invite => invite.NormalizedPhone == normalizedPhone && invite.AcceptedAtUtc == null)
            .ToListAsync(cancellationToken);
        db.StaffInvites.RemoveRange(previous);

        var roles = NormalizeRoles(roleNames);
        var now = timeProvider.GetUtcNow();
        var inviteId = Guid.NewGuid();
        var code = codeGenerator.Generate();
        var expiresAtUtc = now + InviteLifetime;
        var trimmedEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();

        db.StaffInvites.Add(new StaffInviteEntity
        {
            StaffInviteId = inviteId,
            OrganizationId = organizationId,
            BranchId = branchId,
            UserName = userName.Trim(),
            NormalizedUserName = normalizedUserName,
            DisplayName = displayName.Trim(),
            PhoneNumber = "+" + normalizedPhone,
            NormalizedPhone = normalizedPhone,
            Email = trimmedEmail,
            RoleNamesCsv = string.Join(',', roles),
            CodeHash = codeHasher.Hash(code),
            CreatedAtUtc = now,
            ExpiresAtUtc = expiresAtUtc,
        });
        await db.SaveChangesAsync(cancellationToken);

        var tokens = new Dictionary<string, string>
        {
            ["displayName"] = displayName.Trim(),
            ["code"] = code,
        };

        await notifications.SendAsync(
            new NotificationRequest(
                TemplateKey: NotificationTemplateKeys.StaffInviteSms,
                Category: NotificationCategory.Transactional,
                Recipient: new NotificationRecipient(
                    Locale: options.DefaultLocale, PhoneNumber: "+" + normalizedPhone),
                Tokens: tokens,
                IdempotencyKey: $"staff-invite-sms:{inviteId:N}",
                OrganizationId: organizationId,
                BranchId: branchId,
                PreferredChannels: [NotificationChannel.Sms]),
            cancellationToken);

        // Почта — довесок, а не путь: она есть не у каждого администратора зала.
        if (trimmedEmail is not null)
        {
            await notifications.SendAsync(
                new NotificationRequest(
                    TemplateKey: NotificationTemplateKeys.StaffInvite,
                    Category: NotificationCategory.Transactional,
                    Recipient: new NotificationRecipient(
                        Locale: options.DefaultLocale, EmailAddress: trimmedEmail),
                    Tokens: tokens,
                    IdempotencyKey: $"staff-invite:{inviteId:N}",
                    OrganizationId: organizationId,
                    BranchId: branchId),
                cancellationToken);
        }

        return StaffInviteCreateResult.Success(inviteId, code, expiresAtUtc);
    }

    public async Task<StaffInviteAcceptResult> CheckInviteAsync(
        string phoneNumber, string code, CancellationToken cancellationToken)
    {
        var (_, refusal) = await VerifyCodeAsync(phoneNumber, code, cancellationToken);
        return refusal ?? StaffInviteAcceptResult.CodeAccepted();
    }

    public async Task<string> ResolveSignInStepAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        var normalizedPhone = PhoneNumberNormalizer.Normalize(phoneNumber);
        if (normalizedPhone is null)
        {
            return StaffSignInStepNames.Unknown;
        }

        // Тот же отбор, что у входа по номеру: без подтверждённого телефона войти номером нельзя,
        // и спрашивать у такого человека ПИН значит обещать вход, которого не будет.
        var hasPin = await db.StaffUsers.AnyAsync(
            candidate => candidate.NormalizedPhone == normalizedPhone &&
                candidate.PhoneVerifiedAtUtc != null &&
                candidate.IsActive,
            cancellationToken);
        if (hasPin)
        {
            return StaffSignInStepNames.Pin;
        }

        var invite = await LatestPendingInviteAsync(normalizedPhone, cancellationToken);
        if (invite is null)
        {
            return StaffSignInStepNames.Unknown;
        }

        return invite.ExpiresAtUtc <= timeProvider.GetUtcNow() || invite.AttemptCount >= MaxAttempts
            ? StaffSignInStepNames.InviteExpired
            : StaffSignInStepNames.InviteCode;
    }

    public async Task<StaffInviteAcceptResult> AcceptInviteAsync(
        string phoneNumber, string code, string password, CancellationToken cancellationToken)
    {
        var (invite, refusal) = await VerifyCodeAsync(phoneNumber, code, cancellationToken);
        if (refusal is not null)
        {
            return refusal;
        }

        var now = timeProvider.GetUtcNow();
        var alreadyExists = await db.StaffUsers.AnyAsync(
            user => user.OrganizationId == invite.OrganizationId && user.NormalizedUserName == invite.NormalizedUserName,
            cancellationToken);
        if (alreadyExists)
        {
            return StaffInviteAcceptResult.Failed("A staff user with this username already exists in the organization.");
        }

        var planLimit = await planLimitGuard.CheckStaffUserAsync(
            invite.OrganizationId, invite.BranchId, cancellationToken, excludingInviteId: invite.StaffInviteId);
        if (planLimit is not null)
        {
            return StaffInviteAcceptResult.PlanLimitReached(planLimit);
        }

        var staffUser = new StaffUserEntity
        {
            StaffUserId = Guid.NewGuid(),
            OrganizationId = invite.OrganizationId,
            UserName = invite.UserName,
            NormalizedUserName = invite.NormalizedUserName,
            DisplayName = invite.DisplayName,
            Email = invite.Email,
            // Телефон приходит подтверждённым: номер завёл руководитель, а код — из его рук или из
            // SMS на этот номер — подтверждает, что вводит тот самый человек. Иначе приглашённый
            // остался бы без входа по номеру — того самого, которым входят все.
            Phone = invite.PhoneNumber,
            NormalizedPhone = invite.NormalizedPhone,
            PhoneVerifiedAtUtc = now,
            IsActive = true,
            CreatedAtUtc = now,
        };
        staffUser.PasswordHash = passwordHasher.HashPassword(staffUser, password);
        db.StaffUsers.Add(staffUser);

        foreach (var roleName in invite.RoleNamesCsv.Split(RoleSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            db.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
            {
                StaffRoleAssignmentId = Guid.NewGuid(),
                StaffUserId = staffUser.StaffUserId,
                OrganizationId = invite.OrganizationId,
                BranchId = invite.BranchId,
                RoleName = roleName,
            });
        }

        invite.AcceptedAtUtc = now;
        invite.AcceptedByStaffUserId = staffUser.StaffUserId;
        await db.SaveChangesAsync(cancellationToken);

        return StaffInviteAcceptResult.Success(invite.OrganizationId, invite.UserName, staffUser.StaffUserId);
    }

    private Task<StaffInviteEntity?> LatestPendingInviteAsync(string normalizedPhone, CancellationToken cancellationToken) =>
        db.StaffInvites
            .Where(candidate => candidate.NormalizedPhone == normalizedPhone && candidate.AcceptedAtUtc == null)
            .OrderByDescending(candidate => candidate.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<(StaffInviteEntity Invite, StaffInviteAcceptResult? Refusal)> VerifyCodeAsync(
        string phoneNumber, string code, CancellationToken cancellationToken)
    {
        var normalizedPhone = PhoneNumberNormalizer.Normalize(phoneNumber);
        if (normalizedPhone is null || string.IsNullOrWhiteSpace(code))
        {
            return (null!, StaffInviteAcceptResult.NoActiveInvite());
        }

        var invite = await LatestPendingInviteAsync(normalizedPhone, cancellationToken);
        if (invite is null)
        {
            return (null!, StaffInviteAcceptResult.NoActiveInvite());
        }

        if (invite.ExpiresAtUtc <= timeProvider.GetUtcNow())
        {
            return (invite, StaffInviteAcceptResult.Expired());
        }

        // Потолок попыток проверяется до сверки кода: иначе верный код после трёх промахов
        // пускал бы, и счётчик не значил бы ничего.
        if (invite.AttemptCount >= MaxAttempts)
        {
            return (invite, StaffInviteAcceptResult.TooManyAttempts());
        }

        if (codeHasher.Hash(PhoneOtpCode.KeepDigits(code)) != invite.CodeHash)
        {
            invite.AttemptCount++;
            await db.SaveChangesAsync(cancellationToken);
            return (invite, StaffInviteAcceptResult.InvalidCode(Math.Max(0, MaxAttempts - invite.AttemptCount)));
        }

        return (invite, null);
    }

    private static IReadOnlyList<string> NormalizeRoles(IReadOnlyList<string> roleNames) =>
        roleNames
            .Select(role => role.Trim())
            .Where(role => role.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToList();

}
