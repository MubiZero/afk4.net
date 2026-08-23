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
/// Приглашение сотрудника по номеру телефона. Владелец называет номер и роли, человеку уходит SMS
/// с шестизначным кодом, он вводит код и придумывает себе пароль — и получает счёт с уже
/// подтверждённым телефоном, то есть входит номером, как все остальные сотрудники.
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

    private const int MaxAttempts = 3;

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
            return StaffInviteCreateResult.Failed("A valid phone number is required to send the invite.");
        }

        var normalizedUserName = userName.Trim().ToUpperInvariant();
        var alreadyExists = await db.StaffUsers.AnyAsync(
            user => user.OrganizationId == organizationId && user.NormalizedUserName == normalizedUserName,
            cancellationToken);
        if (alreadyExists)
        {
            return StaffInviteCreateResult.Failed("A staff user with this username already exists in the organization.");
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
            return StaffInviteCreateResult.Failed("This phone number already belongs to a staff member.");
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

    public async Task<StaffInviteAcceptResult> AcceptInviteAsync(
        string phoneNumber, string code, string password, CancellationToken cancellationToken)
    {
        var normalizedPhone = PhoneNumberNormalizer.Normalize(phoneNumber);
        if (normalizedPhone is null || string.IsNullOrWhiteSpace(code))
        {
            return StaffInviteAcceptResult.NoActiveInvite();
        }

        var now = timeProvider.GetUtcNow();
        var invite = await db.StaffInvites
            .Where(candidate => candidate.NormalizedPhone == normalizedPhone && candidate.AcceptedAtUtc == null)
            .OrderByDescending(candidate => candidate.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (invite is null)
        {
            return StaffInviteAcceptResult.NoActiveInvite();
        }

        if (invite.ExpiresAtUtc <= now)
        {
            return StaffInviteAcceptResult.Expired();
        }

        // Потолок попыток проверяется до сверки кода: иначе верный код после трёх промахов
        // пускал бы, и счётчик не значил бы ничего.
        if (invite.AttemptCount >= MaxAttempts)
        {
            return StaffInviteAcceptResult.TooManyAttempts();
        }

        if (codeHasher.Hash(PhoneOtpCode.KeepDigits(code)) != invite.CodeHash)
        {
            invite.AttemptCount++;
            await db.SaveChangesAsync(cancellationToken);
            return StaffInviteAcceptResult.InvalidCode(Math.Max(0, MaxAttempts - invite.AttemptCount));
        }

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
            // Телефон приходит подтверждённым: код из SMS и есть доказательство, что номер его.
            // Иначе приглашённый остался бы без входа по номеру — того самого, которым входят все.
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

        return StaffInviteAcceptResult.Success(invite.OrganizationId, invite.UserName);
    }

    private static IReadOnlyList<string> NormalizeRoles(IReadOnlyList<string> roleNames) =>
        roleNames
            .Select(role => role.Trim())
            .Where(role => role.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToList();

}
