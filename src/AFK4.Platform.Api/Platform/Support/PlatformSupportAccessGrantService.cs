using System.Security.Cryptography;
using AFK4.Platform.Api.Configuration;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Identity;
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

    public async Task<PlatformSupportContext?> ValidateAsync(
        HttpContext httpContext,
        Guid organizationId,
        string requiredPermission,
        IPlatformAdminContextAccessor platformContextAccessor,
        CancellationToken cancellationToken)
    {
        var platform = platformContextAccessor.Current;
        var metadata = httpContext.GetEndpoint()?.Metadata.GetMetadata<PlatformSupportAccessMetadata>();
        if (platform is null || metadata?.Permission != requiredPermission
            || !platform.Permissions.Contains(AFK4.Shared.Contracts.Platform.Auth.PlatformAdminPermissionNames.UseSupportAccess)
            || !Guid.TryParse(httpContext.Request.Headers[GrantHeaderName].SingleOrDefault(), out var grantId))
            return null;

        var now = timeProvider.GetUtcNow();
        var grant = await dbContext.PlatformSupportAccessGrants.AsNoTracking().SingleOrDefaultAsync(
            x => x.GrantId == grantId
                && x.PlatformAdminUserId == platform.PlatformAdminUserId
                && x.OrganizationId == organizationId
                && x.RevokedAtUtc == null
                && x.ExpiresAtUtc > now,
            cancellationToken);
        return grant is null ? null : new PlatformSupportContext(
            grant.GrantId, grant.PlatformAdminUserId, grant.OrganizationId,
            grant.Reason, requiredPermission, grant.ExpiresAtUtc);
    }

    public async Task<PlatformSupportContext?> ValidateBranchAsync(
        HttpContext httpContext,
        Guid branchId,
        string requiredPermission,
        IPlatformAdminContextAccessor platformContextAccessor,
        CancellationToken cancellationToken)
    {
        var organizationId = await dbContext.Branches.AsNoTracking()
            .Where(branch => branch.BranchId == branchId)
            .Select(branch => (Guid?)branch.OrganizationId)
            .SingleOrDefaultAsync(cancellationToken);
        return organizationId is null
            ? null
            : await ValidateAsync(httpContext, organizationId.Value, requiredPermission, platformContextAccessor, cancellationToken);
    }

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

        var now = timeProvider.GetUtcNow();
        var ticketHash = Hash(ticket);
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

        var sessionToken = GenerateSecret();
        grant.TicketUsedAtUtc = now;
        grant.SessionTokenHash = Hash(sessionToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleAsync(candidate => candidate.OrganizationId == grant.OrganizationId, cancellationToken);

        return new PlatformSupportSessionDto(
            sessionToken,
            grant.OrganizationId,
            organization.Name,
            grant.Reason,
            grant.ExpiresAtUtc,
            PlatformSupportWritableAreas.All);
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
                grant.ExpiresAtUtc);
    }

    private static string GenerateSecret() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    private static byte[] Hash(string secret) =>
        SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret));
}
