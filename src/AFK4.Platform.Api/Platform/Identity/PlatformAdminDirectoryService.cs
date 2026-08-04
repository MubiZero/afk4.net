using System.Data;
using System.Security.Cryptography;
using System.Text;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Identity;

public sealed class PlatformAdminDirectoryService(PlatformDbContext dbContext, TimeProvider timeProvider)
{
    // Excludes visually similar characters (0, O, 1, l) so the code stays readable when typed by hand.
    private const string InvitationCodeAlphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private const int InvitationCodeLength = 32;

    public async Task<IReadOnlyList<PlatformAdminListItem>> ListAsync(CancellationToken cancellationToken)
    {
        var admins = await dbContext.PlatformAdminUsers
            .AsNoTracking()
            .OrderBy(admin => admin.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);

        return admins.Select(ToListItem).ToArray();
    }

    public async Task<IReadOnlyList<PlatformAdminInvitationDto>> ListInvitationsAsync(CancellationToken cancellationToken)
    {
        var invitations = await dbContext.PlatformAdminInvitations
            .AsNoTracking()
            .OrderByDescending(invitation => invitation.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);

        return invitations.Select(ToInvitationDto).ToArray();
    }

    public async Task<(CreatePlatformAdminInvitationResponse? Response, PlatformAdminDirectoryError Error)> InviteAsync(
        Guid actorId,
        CreatePlatformAdminInvitationRequest request,
        CancellationToken cancellationToken)
    {
        if (!PlatformAdminPermissionCatalog.IsKnownRole(request.Role))
        {
            return (null, PlatformAdminDirectoryError.UnknownRole);
        }

        var now = timeProvider.GetUtcNow();
        var code = GenerateInvitationCode();
        var entity = new PlatformAdminInvitationEntity
        {
            InvitationId = Guid.NewGuid(),
            CodeHash = HashCode(code),
            Role = request.Role,
            Status = "pending",
            ExpiresAtUtc = now.AddHours(request.LifetimeHours),
            CreatedByPlatformAdminUserId = actorId,
            CreatedAtUtc = now
        };

        dbContext.PlatformAdminInvitations.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (new CreatePlatformAdminInvitationResponse(ToInvitationDto(entity), code), PlatformAdminDirectoryError.None);
    }

    public Task<(PlatformAdminListItem? Item, PlatformAdminDirectoryError Error)> UpdateAsync(
        Guid actorId,
        Guid targetId,
        UpdatePlatformAdminRequest request,
        CancellationToken cancellationToken)
    {
        // The "at least one active platform_admin survives" check below is read-then-write: it reads
        // every other admin's IsActive/role, then writes the target row. Two concurrent UpdateAsync
        // calls demoting/disabling two DIFFERENT full admins would each see the other as still active
        // under Read Committed and both pass, leaving zero full admins. Serializable closes that gap —
        // Postgres aborts (at least) one side with a serialization failure, which we turn into the
        // generic Conflict error instead of letting it surface as a raw 500. It is deliberately NOT
        // reported as LastFullAdmin: SSI can abort the "innocent" side of the race too, and that side's
        // change may have nothing to do with the LastFullAdmin rule at all.
        return ExecuteInSerializableTransactionAsync(
            () => UpdateCoreAsync(actorId, targetId, request, cancellationToken),
            cancellationToken);
    }

    private async Task<(PlatformAdminListItem? Item, PlatformAdminDirectoryError Error)> UpdateCoreAsync(
        Guid actorId,
        Guid targetId,
        UpdatePlatformAdminRequest request,
        CancellationToken cancellationToken)
    {
        var target = await dbContext.PlatformAdminUsers
            .SingleOrDefaultAsync(admin => admin.PlatformAdminUserId == targetId, cancellationToken);

        if (target is null)
        {
            return (null, PlatformAdminDirectoryError.NotFound);
        }

        if (request.Role is not null && !PlatformAdminPermissionCatalog.IsKnownRole(request.Role))
        {
            return (null, PlatformAdminDirectoryError.UnknownRole);
        }

        var currentRoles = OpaquePlatformAdminTokenService.ParseRoles(target.RolesJson);
        var currentRole = PrimaryRole(currentRoles);
        var resultingRole = request.Role ?? currentRole;
        var resultingIsActive = request.IsActive ?? target.IsActive;

        var wasActiveFullAdmin = target.IsActive && IsFullAdminRole(currentRole);
        var staysActiveFullAdmin = resultingIsActive && IsFullAdminRole(resultingRole);

        // A full-admin losing that status (via role change or deactivation) must leave at least
        // one other active platform_admin behind — otherwise nobody could manage the panel anymore.
        // This lockout check is evaluated ahead of self-demotion: it is the more specific, more
        // severe invariant, and it must win when a self-targeted change would trip both at once
        // (e.g. the sole admin deactivating themselves).
        if (wasActiveFullAdmin && !staysActiveFullAdmin)
        {
            var anotherFullAdminExists = await dbContext.PlatformAdminUsers
                .AsNoTracking()
                .Where(admin => admin.PlatformAdminUserId != targetId && admin.IsActive)
                .ToArrayAsync(cancellationToken);

            var stillHasFullAdmin = anotherFullAdminExists.Any(
                admin => IsFullAdminRole(PrimaryRole(OpaquePlatformAdminTokenService.ParseRoles(admin.RolesJson))));

            if (!stillHasFullAdmin)
            {
                return (null, PlatformAdminDirectoryError.LastFullAdmin);
            }
        }

        if (actorId == targetId)
        {
            var isRoleDowngrade = request.Role is not null
                && !string.Equals(request.Role, currentRole, StringComparison.OrdinalIgnoreCase)
                && IsRoleDowngrade(currentRole, request.Role);

            if (isRoleDowngrade || request.IsActive == false)
            {
                return (null, PlatformAdminDirectoryError.SelfDemotion);
            }
        }

        target.RolesJson = OpaquePlatformAdminTokenService.SerializeRoles([resultingRole]);
        target.IsActive = resultingIsActive;
        target.UpdatedAtUtc = timeProvider.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);

        return (ToListItem(target), PlatformAdminDirectoryError.None);
    }

    private async Task<(PlatformAdminListItem? Item, PlatformAdminDirectoryError Error)> ExecuteInSerializableTransactionAsync(
        Func<Task<(PlatformAdminListItem? Item, PlatformAdminDirectoryError Error)>> action,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            // The InMemory provider used by tests doesn't support transactions or snapshot
            // isolation at all, so there's nothing to wrap — and nothing that can race, since
            // InMemory queries never observe another in-flight save anyway.
            return await action();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var result = await action();
            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch (Exception exception) when (RelationalFailureClassifier.IsSerializationFailure(exception))
        {
            // Postgres's serializable snapshot isolation can abort either side of a race, including
            // one whose own change had nothing to do with the LastFullAdmin rule (e.g. it lost only
            // because it shared a read/write set with the other transaction). Reporting it as
            // LastFullAdmin would be a false claim about the cause — the caller would go looking for
            // a lockout problem that doesn't exist. Report the generic, honest outcome instead: retry.
            await RelationalFailureClassifier.RollbackIfActiveAsync(transaction, cancellationToken);
            dbContext.ChangeTracker.Clear();
            return (null, PlatformAdminDirectoryError.Conflict);
        }
    }

    public async Task<PlatformAdminDirectoryError> RevokeInvitationAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        var invitation = await dbContext.PlatformAdminInvitations
            .SingleOrDefaultAsync(candidate => candidate.InvitationId == invitationId, cancellationToken);

        if (invitation is null)
        {
            return PlatformAdminDirectoryError.NotFound;
        }

        if (invitation.Status == "pending")
        {
            invitation.Status = "revoked";
            invitation.RevokedAtUtc = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return PlatformAdminDirectoryError.None;
    }

    // Finds the invitation by hashed code among pending, non-expired, non-revoked candidates and,
    // on success, creates the admin account and marks the invitation accepted. Any lookup failure
    // (code unknown / expired / revoked / already accepted) collapses to the same
    // InvalidInvitationCode error — the caller must not be able to tell those apart, or invitation
    // codes become guessable by probing response differences.
    public async Task<(PlatformAdminUserEntity? User, PlatformAdminDirectoryError Error)> AcceptInvitationAsync(
        AcceptPlatformAdminInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var codeHash = HashCode(request.Code);

        var candidates = await dbContext.PlatformAdminInvitations
            .Where(invitation => invitation.Status == "pending"
                && invitation.RevokedAtUtc == null
                && invitation.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);
        var invitation = candidates.SingleOrDefault(candidate => candidate.CodeHash.SequenceEqual(codeHash));

        if (invitation is null)
        {
            return (null, PlatformAdminDirectoryError.InvalidInvitationCode);
        }

        var normalizedUserName = request.UserName.Trim().ToUpperInvariant();
        var userNameTaken = await dbContext.PlatformAdminUsers
            .AnyAsync(admin => admin.NormalizedUserName == normalizedUserName, cancellationToken);
        if (userNameTaken)
        {
            return (null, PlatformAdminDirectoryError.UserNameTaken);
        }

        var admin = new PlatformAdminUserEntity
        {
            PlatformAdminUserId = Guid.NewGuid(),
            UserName = request.UserName.Trim(),
            NormalizedUserName = normalizedUserName,
            DisplayName = request.DisplayName.Trim(),
            RolesJson = OpaquePlatformAdminTokenService.SerializeRoles([invitation.Role]),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        admin.PasswordHash = new PasswordHasher<PlatformAdminUserEntity>().HashPassword(admin, request.Password);

        dbContext.PlatformAdminUsers.Add(admin);

        invitation.Status = "accepted";
        invitation.AcceptedAtUtc = now;
        invitation.AcceptedPlatformAdminUserId = admin.PlatformAdminUserId;

        await dbContext.SaveChangesAsync(cancellationToken);

        return (admin, PlatformAdminDirectoryError.None);
    }

    private static bool IsFullAdminRole(string role)
    {
        return string.Equals(role, PlatformAdminRoleNames.PlatformAdmin, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRoleDowngrade(string fromRole, string toRole)
    {
        var fromPermissionCount = PlatformAdminPermissionCatalog.GetPermissions([fromRole]).Count;
        var toPermissionCount = PlatformAdminPermissionCatalog.GetPermissions([toRole]).Count;
        return toPermissionCount < fromPermissionCount;
    }

    private static string PrimaryRole(IReadOnlySet<string> roles)
    {
        if (roles.Contains(PlatformAdminRoleNames.PlatformAdmin))
        {
            return PlatformAdminRoleNames.PlatformAdmin;
        }

        return roles.OrderBy(role => role, StringComparer.Ordinal).FirstOrDefault() ?? string.Empty;
    }

    private static PlatformAdminListItem ToListItem(PlatformAdminUserEntity admin)
    {
        var role = PrimaryRole(OpaquePlatformAdminTokenService.ParseRoles(admin.RolesJson));
        return new PlatformAdminListItem(
            admin.PlatformAdminUserId,
            admin.UserName,
            admin.DisplayName,
            role,
            admin.IsActive,
            admin.TotpEnabledAtUtc.HasValue,
            admin.LastSignInAtUtc,
            admin.CreatedAtUtc);
    }

    private static PlatformAdminInvitationDto ToInvitationDto(PlatformAdminInvitationEntity invitation)
    {
        return new PlatformAdminInvitationDto(
            invitation.InvitationId,
            invitation.Role,
            invitation.Status,
            invitation.ExpiresAtUtc,
            invitation.CreatedAtUtc);
    }

    private static string GenerateInvitationCode()
    {
        var builder = new StringBuilder(InvitationCodeLength);
        for (var i = 0; i < InvitationCodeLength; i++)
        {
            var index = RandomNumberGenerator.GetInt32(InvitationCodeAlphabet.Length);
            builder.Append(InvitationCodeAlphabet[index]);
        }

        return builder.ToString();
    }

    private static byte[] HashCode(string code)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(code));
    }
}
