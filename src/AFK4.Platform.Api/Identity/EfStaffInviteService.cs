using System.Security.Cryptography;
using System.Text;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Identity;

/// <summary>
/// EF Core <see cref="IStaffInviteService"/>. Issues opaque single-use invite tokens (RNG ->
/// SHA-256 stored, plaintext emailed once — mirrors <c>OpaqueStaffTokenService</c> /
/// <see cref="EfStaffPasswordResetService"/>) and, on accept, creates the active staff user with
/// their chosen password and the invited branch roles.
/// </summary>
public sealed class EfStaffInviteService(
    PlatformDbContext db,
    INotificationService notifications,
    TimeProvider timeProvider,
    IOptions<NotificationOptions> options) : IStaffInviteService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(7);

    private static readonly char[] RoleSeparator = [','];

    private readonly PasswordHasher<StaffUserEntity> passwordHasher = new();
    private readonly NotificationOptions options = options.Value;

    public async Task<StaffInviteCreateResult> CreateInviteAsync(
        Guid organizationId,
        Guid branchId,
        string userName,
        string displayName,
        string email,
        IReadOnlyList<string> roleNames,
        CancellationToken cancellationToken)
    {
        var normalizedUserName = userName.Trim().ToUpperInvariant();
        var alreadyExists = await db.StaffUsers.AnyAsync(
            user => user.OrganizationId == organizationId && user.NormalizedUserName == normalizedUserName,
            cancellationToken);
        if (alreadyExists)
        {
            return StaffInviteCreateResult.Failed("A staff user with this username already exists in the organization.");
        }

        var roles = NormalizeRoles(roleNames);
        var now = timeProvider.GetUtcNow();
        var inviteId = Guid.NewGuid();
        var plaintext = $"{inviteId:N}.{Convert.ToHexString(RandomNumberGenerator.GetBytes(32))}";
        var expiresAtUtc = now + TokenLifetime;

        db.StaffInvites.Add(new StaffInviteEntity
        {
            StaffInviteId = inviteId,
            OrganizationId = organizationId,
            BranchId = branchId,
            UserName = userName.Trim(),
            NormalizedUserName = normalizedUserName,
            DisplayName = displayName.Trim(),
            Email = email.Trim(),
            RoleNamesCsv = string.Join(',', roles),
            TokenHash = HashToken(plaintext),
            CreatedAtUtc = now,
            ExpiresAtUtc = expiresAtUtc,
        });
        await db.SaveChangesAsync(cancellationToken);

        var request = new NotificationRequest(
            TemplateKey: NotificationTemplateKeys.StaffInvite,
            Category: NotificationCategory.Transactional,
            Recipient: new NotificationRecipient(Locale: options.DefaultLocale, EmailAddress: email.Trim()),
            Tokens: new Dictionary<string, string>
            {
                ["displayName"] = displayName.Trim(),
                ["code"] = plaintext,
            },
            IdempotencyKey: $"staff-invite:{inviteId:N}",
            OrganizationId: organizationId,
            BranchId: branchId);
        await notifications.SendAsync(request, cancellationToken);

        return StaffInviteCreateResult.Success(inviteId, plaintext, expiresAtUtc);
    }

    public async Task<StaffInviteAcceptResult> AcceptInviteAsync(string token, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        var now = timeProvider.GetUtcNow();
        var tokenHash = HashToken(token);

        var candidates = await db.StaffInvites
            .Where(invite => invite.AcceptedAtUtc == null && invite.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);
        var invite = candidates.SingleOrDefault(candidate => candidate.TokenHash.SequenceEqual(tokenHash));
        if (invite is null)
        {
            return StaffInviteAcceptResult.Failed("The invite is invalid or has expired.");
        }

        var alreadyExists = await db.StaffUsers.AnyAsync(
            user => user.OrganizationId == invite.OrganizationId && user.NormalizedUserName == invite.NormalizedUserName,
            cancellationToken);
        if (alreadyExists)
        {
            return StaffInviteAcceptResult.Failed("A staff user with this username already exists in the organization.");
        }

        var staffUser = new StaffUserEntity
        {
            StaffUserId = Guid.NewGuid(),
            OrganizationId = invite.OrganizationId,
            UserName = invite.UserName,
            NormalizedUserName = invite.NormalizedUserName,
            DisplayName = invite.DisplayName,
            Email = invite.Email,
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

    private static byte[] HashToken(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
