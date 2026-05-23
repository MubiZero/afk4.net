using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Health;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Tenancy;

public sealed class EfPlatformTenantHealthService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IPlatformTenantHealthService
{
    private const int RecentErrorWindowDays = 7;
    private const int RecentErrorLimit = 10;
    private const int MessagePreviewLength = 240;

    public async Task<TenantHealthDto?> GetAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(org => org.OrganizationId == organizationId, cancellationToken);
        if (organization is null)
        {
            return null;
        }

        var branchCount = await dbContext.Branches
            .CountAsync(branch => branch.OrganizationId == organizationId, cancellationToken);
        var deviceCount = await dbContext.Devices
            .CountAsync(device => device.OrganizationId == organizationId, cancellationToken);
        var activeStaffUserCount = await dbContext.StaffUsers
            .CountAsync(staff => staff.OrganizationId == organizationId && staff.IsActive, cancellationToken);

        var latestStaffSignInAtUtc = await dbContext.StaffAccessTokens
            .Where(token => token.OrganizationId == organizationId)
            .OrderByDescending(token => token.CreatedAtUtc)
            .Select(token => (DateTimeOffset?)token.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var latestMigration = await TryGetLatestMigrationAsync(cancellationToken);

        var since = timeProvider.GetUtcNow().AddDays(-RecentErrorWindowDays);
        var recentErrorCount = await dbContext.AuditRecords
            .CountAsync(
                record =>
                    record.OrganizationId == organizationId &&
                    record.Outcome == AuditOutcome.Denied &&
                    record.CreatedAtUtc >= since,
                cancellationToken);
        var recentErrorEntities = await dbContext.AuditRecords
            .AsNoTracking()
            .Where(record =>
                record.OrganizationId == organizationId &&
                record.Outcome == AuditOutcome.Denied &&
                record.CreatedAtUtc >= since)
            .OrderByDescending(record => record.CreatedAtUtc)
            .Take(RecentErrorLimit)
            .ToListAsync(cancellationToken);
        var recentErrors = recentErrorEntities
            .Select(record => new TenantHealthErrorDto(
                CreatedAtUtc: record.CreatedAtUtc,
                Source: record.SourceApp,
                Action: record.Action,
                Outcome: record.Outcome,
                Message: TruncatePreview(record.DetailsJson)))
            .ToList();

        return new TenantHealthDto(
            OrganizationId: organization.OrganizationId,
            Status: organization.Status,
            BranchCount: branchCount,
            DeviceCount: deviceCount,
            ActiveStaffUserCount: activeStaffUserCount,
            LatestStaffSignInAtUtc: latestStaffSignInAtUtc,
            LatestMigration: latestMigration,
            RecentErrorCount: recentErrorCount,
            RecentErrors: recentErrors);
    }

    private async Task<string?> TryGetLatestMigrationAsync(CancellationToken cancellationToken)
    {
        try
        {
            var migrations = await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken);
            return migrations
                .OrderBy(name => name, StringComparer.Ordinal)
                .LastOrDefault();
        }
        catch
        {
            // EF in-memory and other non-relational providers don't support migration history.
            return null;
        }
    }

    private static string? TruncatePreview(string? details)
    {
        if (string.IsNullOrEmpty(details))
        {
            return null;
        }

        return details.Length <= MessagePreviewLength
            ? details
            : string.Concat(details.AsSpan(0, MessagePreviewLength), "...");
    }
}
