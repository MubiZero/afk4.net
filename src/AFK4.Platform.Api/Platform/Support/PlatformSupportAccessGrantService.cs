using System.Data;
using System.Security.Cryptography;
using AFK4.Platform.Api.Configuration;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Support;

public sealed class PlatformSupportAccessGrantService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider,
    IOptions<SupportAccessOptions> supportAccessOptions)
{
    public const string GrantHeaderName = "X-AFK4-Support-Access-Grant";

    // Билет живёт 60 секунд: он нужен ровно на переход между двумя вкладками.
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromSeconds(60);

    public async Task<PlatformSupportAccessGrantDto?> CreateAsync(
        Guid platformAdminUserId,
        CreatePlatformSupportAccessGrantRequest request,
        CancellationToken cancellationToken)
    {
        var reason = request.Reason.Trim();
        if (reason.Length < 10 || reason.Length > 500 || request.LifetimeMinutes is < 1 or > 30)
            return null;
        if (!await dbContext.Organizations.AnyAsync(x => x.OrganizationId == request.OrganizationId, cancellationToken))
            return null;

        var now = timeProvider.GetUtcNow();
        var entity = new PlatformSupportAccessGrantEntity
        {
            GrantId = Guid.NewGuid(),
            PlatformAdminUserId = platformAdminUserId,
            OrganizationId = request.OrganizationId,
            Reason = reason,
            IssuedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(request.LifetimeMinutes)
        };
        dbContext.PlatformSupportAccessGrants.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PlatformSupportAccessGrantEntity?> RevokeAsync(
        Guid grantId,
        Guid platformAdminUserId,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.PlatformSupportAccessGrants.SingleOrDefaultAsync(
            x => x.GrantId == grantId && x.PlatformAdminUserId == platformAdminUserId,
            cancellationToken);
        if (entity is null || entity.RevokedAtUtc is not null) return null;
        entity.RevokedAtUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private static PlatformSupportAccessGrantDto ToDto(PlatformSupportAccessGrantEntity entity) => new(
        entity.GrantId, entity.OrganizationId, entity.Reason, entity.IssuedAtUtc, entity.ExpiresAtUtc, entity.RevokedAtUtc);

    public async Task<PlatformSupportAccessGrantIssue?> IssueAsync(
        Guid platformAdminUserId,
        CreatePlatformSupportAccessGrantRequest request,
        CancellationToken cancellationToken)
    {
        var grant = await CreateAsync(platformAdminUserId, request, cancellationToken);
        if (grant is null)
        {
            return null;
        }

        var ticket = GenerateSecret();
        var entity = await dbContext.PlatformSupportAccessGrants
            .SingleAsync(candidate => candidate.GrantId == grant.GrantId, cancellationToken);
        entity.TicketHash = Hash(ticket);
        await dbContext.SaveChangesAsync(cancellationToken);

        var baseUrl = supportAccessOptions.Value.OrganizationAdminBaseUrl.TrimEnd('/');
        return new PlatformSupportAccessGrantIssue(grant, ticket, $"{baseUrl}/support-access?ticket={ticket}");
    }

    public async Task<PlatformSupportSessionDto?> RedeemTicketAsync(
        string ticket,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ticket))
        {
            return null;
        }

        var ticketHash = Hash(ticket);
        var sessionToken = GenerateSecret();

        var grant = await ExecuteInSerializableTransactionAsync(
            () => TryClaimTicketAsync(ticketHash, sessionToken, cancellationToken),
            cancellationToken);

        if (grant is null)
        {
            return null;
        }

        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleAsync(candidate => candidate.OrganizationId == grant.OrganizationId, cancellationToken);
        var branches = await LoadBranchesAsync(grant.OrganizationId, cancellationToken);

        return new PlatformSupportSessionDto(
            sessionToken,
            grant.OrganizationId,
            organization.Name,
            grant.Reason,
            grant.ExpiresAtUtc,
            PlatformSupportWritableAreas.All,
            branches);
    }

    // GET /api/support-access/session (session self-lookup, e.g. after a tab reload) needs the exact
    // same shape RedeemTicketAsync returns — otherwise a reload under an active support session lands
    // somewhere the shell can't render (no branches). Shares the branch/organization lookup with
    // RedeemTicketAsync rather than re-deriving it.
    public async Task<PlatformSupportSessionDto> DescribeSessionAsync(
        PlatformSupportContext support,
        CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleAsync(candidate => candidate.OrganizationId == support.OrganizationId, cancellationToken);
        var branches = await LoadBranchesAsync(support.OrganizationId, cancellationToken);

        return new PlatformSupportSessionDto(
            support.SessionToken,
            support.OrganizationId,
            organization.Name,
            support.Reason,
            support.ExpiresAtUtc,
            PlatformSupportWritableAreas.All,
            branches);
    }

    private async Task<List<PlatformSupportSessionBranchDto>> LoadBranchesAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        await dbContext.Branches
            .AsNoTracking()
            .Where(branch => branch.OrganizationId == organizationId)
            .OrderBy(branch => branch.Name)
            .Select(branch => new PlatformSupportSessionBranchDto(branch.BranchId, branch.Name))
            .ToListAsync(cancellationToken);

    // Read-then-write on the SAME row two concurrent redemptions target. Under Postgres SERIALIZABLE
    // (see ExecuteInSerializableTransactionAsync), the second transaction to attempt the UPDATE blocks
    // on the first's row lock and, once the first commits, is aborted with a serialization failure
    // instead of silently overwriting TicketUsedAtUtc/SessionTokenHash — so exactly one caller ever
    // gets a non-null grant back for a given ticket.
    private async Task<PlatformSupportAccessGrantEntity?> TryClaimTicketAsync(
        byte[] ticketHash,
        string sessionToken,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var grant = await dbContext.PlatformSupportAccessGrants
            .SingleOrDefaultAsync(
                candidate => candidate.TicketHash == ticketHash
                    && candidate.TicketUsedAtUtc == null
                    && candidate.RevokedAtUtc == null
                    && candidate.ExpiresAtUtc > now,
                cancellationToken);

        if (grant is null || grant.IssuedAtUtc + TicketLifetime < now)
        {
            return null;
        }

        grant.TicketUsedAtUtc = now;
        grant.SessionTokenHash = Hash(sessionToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return grant;
    }

    // Same shape as PlatformAdminDirectoryService.ExecuteInSerializableTransactionAsync: the InMemory
    // provider used by most tests has no transactions or snapshot isolation and never observes a
    // concurrent save, so there's nothing to wrap there. Against real Postgres, a losing concurrent
    // transaction surfaces as a serialization failure, which is mapped to "ticket already used" (null)
    // rather than an unhandled exception.
    private async Task<PlatformSupportAccessGrantEntity?> ExecuteInSerializableTransactionAsync(
        Func<Task<PlatformSupportAccessGrantEntity?>> action,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
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
            await RelationalFailureClassifier.RollbackIfActiveAsync(transaction, cancellationToken);
            dbContext.ChangeTracker.Clear();
            return null;
        }
    }

    public async Task<PlatformSupportContext?> AuthenticateSessionAsync(
        string sessionToken,
        string requiredPermission,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var sessionHash = Hash(sessionToken);
        var grant = await dbContext.PlatformSupportAccessGrants
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.SessionTokenHash == sessionHash
                    && candidate.RevokedAtUtc == null
                    && candidate.ExpiresAtUtc > now,
                cancellationToken);

        return grant is null
            ? null
            : new PlatformSupportContext(
                grant.GrantId,
                grant.PlatformAdminUserId,
                grant.OrganizationId,
                grant.Reason,
                requiredPermission,
                grant.ExpiresAtUtc,
                sessionToken);
    }

    private static string GenerateSecret() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    private static byte[] Hash(string secret) =>
        SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret));
}
